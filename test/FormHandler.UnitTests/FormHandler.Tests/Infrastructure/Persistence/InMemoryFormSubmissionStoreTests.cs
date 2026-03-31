using System;
using System.Linq;
using System.Threading.Tasks;
using FormHandler.Api.Application.Outbox;
using FormHandler.Api.Application.Submissions;
using FormHandler.Api.Domain.Submissions;
using FormHandler.Api.Infrastructure.Persistence;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace FormHandler.Tests.Infrastructure.Persistence
{
    public sealed class InMemoryFormSubmissionStoreTests
    {
        private readonly FakeTimeProvider timeProvider = new();
        private readonly InMemoryFormSubmissionStore store;

        public InMemoryFormSubmissionStoreTests()
        {
            this.store = new InMemoryFormSubmissionStore(this.timeProvider);
        }

        private async Task CycleToDeadLetterAsync(Guid outboxId)
        {
            // Claim 1 has already been done (AttemptCount = 1). Need 4 more claim+fail
            // cycles to reach AttemptCount = 5, after which MarkAsFailedAsync dead-letters.
            for (var iteration = 0; iteration < 4; iteration++)
            {
                await this.store.MarkAsFailedAsync(outboxId, "error", TestContext.Current.CancellationToken);
                this.timeProvider.Advance(TimeSpan.FromHours(1));
                await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken);
            }
        }

        // Submission store

        [Theory, FormHandlerAutoData]
        public async Task SavedSubmissionAppearsInQueryResults(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.store.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);

            var results = await this.store.GetSubmissionsAsync(submission.FormId, TestContext.Current.CancellationToken);

            results.ShouldHaveSingleItem();
        }

        [Fact]
        public async Task NoResultsReturnedForUnknownFormId()
        {
            var results = await this.store.GetSubmissionsAsync("nonexistent", TestContext.Current.CancellationToken);

            results.ShouldBeEmpty();
        }

        [Theory, FormHandlerAutoData]
        public async Task QueryResultsExcludeOtherForms(
            FormSubmission submissionA, FormSubmission submissionB,
            FormSubmissionMetadata metadataA, FormSubmissionMetadata metadataB)
        {
            await this.store.SaveAsync(submissionA, metadataA, TestContext.Current.CancellationToken);
            await this.store.SaveAsync(submissionB, metadataB, TestContext.Current.CancellationToken);

            var results = await this.store.GetSubmissionsAsync(submissionA.FormId, TestContext.Current.CancellationToken);

            results.ShouldHaveSingleItem().FormId.ShouldBe(submissionA.FormId);
        }

        [Fact]
        public async Task UnknownSubmissionIdReturnsNull()
        {
            var result = await this.store.GetSubmissionByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

            result.ShouldBeNull();
        }

        [Theory, FormHandlerAutoData]
        public async Task SubmissionCanBeRetrievedById(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.store.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);

            var saved = (await this.store.GetSubmissionsAsync(submission.FormId, TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
            var result = await this.store.GetSubmissionByIdAsync(saved.Id, TestContext.Current.CancellationToken);

            result.ShouldNotBeNull();
            result.FormId.ShouldBe(submission.FormId);
        }

        // Outbox store

        [Theory, FormHandlerAutoData]
        public async Task SaveCreatesEmailAndWebhookOutboxEntries(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.store.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);

            var entries = await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken);

            entries.Count.ShouldBe(2);
            entries.ShouldContain(entry => entry.Type == "email");
            entries.ShouldContain(entry => entry.Type == "webhook");
        }

        [Theory, FormHandlerAutoData]
        public async Task ClaimingEntriesIncrementsAttemptCount(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.store.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);

            var entries = await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken);

            entries.ShouldAllBe(entry => entry.AttemptCount == 1);
        }

        [Theory, FormHandlerAutoData]
        public async Task RecentlyClaimedEntriesAreWithheldDuringBackoff(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.store.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);
            await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken);

            var entries = await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken);

            entries.ShouldBeEmpty();
        }

        [Theory, FormHandlerAutoData]
        public async Task ProcessedEntryIsNeverClaimedAgain(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.store.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);
            var entry = (await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken)).First(e => e.Type == "email");
            await this.store.MarkAsSentAsync(entry.OutboxId, TestContext.Current.CancellationToken);

            this.timeProvider.Advance(TimeSpan.FromHours(1));
            var entries = await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken);

            entries.ShouldNotContain(e => e.OutboxId == entry.OutboxId);
        }

        [Theory, FormHandlerAutoData]
        public async Task FailedEntryWithRemainingAttemptsSchedulesRetry(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.store.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);
            var entry = (await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken)).First(e => e.Type == "email");

            var result = await this.store.MarkAsFailedAsync(entry.OutboxId, "error", TestContext.Current.CancellationToken);

            result.ShouldBeOfType<OutboxFailureResult.WillRetry>();
        }

        [Theory, FormHandlerAutoData]
        public async Task EntryIsDeadLetteredAfterFiveFailures(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.store.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);
            var outboxId = (await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken)).First(e => e.Type == "email").OutboxId;

            await this.CycleToDeadLetterAsync(outboxId);

            var result = await this.store.MarkAsFailedAsync(outboxId, "fatal error", TestContext.Current.CancellationToken);

            result.ShouldBeOfType<OutboxFailureResult.RetriesExhausted>();
        }

        [Theory, FormHandlerAutoData]
        public async Task DeadLetteredEntryIsNeverClaimedAgain(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.store.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);
            var outboxId = (await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken)).First(e => e.Type == "email").OutboxId;

            await this.CycleToDeadLetterAsync(outboxId);
            await this.store.MarkAsFailedAsync(outboxId, "fatal error", TestContext.Current.CancellationToken);

            this.timeProvider.Advance(TimeSpan.FromHours(1));
            var entries = await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken);

            entries.ShouldNotContain(e => e.OutboxId == outboxId);
        }

        // Concurrency

        [Theory, FormHandlerAutoData]
        public async Task ConcurrentSavesDoNotLoseSubmissions(
            FormSubmission submission,
            FormSubmissionMetadata metadata1, FormSubmissionMetadata metadata2, FormSubmissionMetadata metadata3)
        {
            await Task.WhenAll(
                this.store.SaveAsync(submission, metadata1, TestContext.Current.CancellationToken),
                this.store.SaveAsync(submission, metadata2, TestContext.Current.CancellationToken),
                this.store.SaveAsync(submission, metadata3, TestContext.Current.CancellationToken));

            var results = await this.store.GetSubmissionsAsync(submission.FormId, TestContext.Current.CancellationToken);

            results.Count.ShouldBe(3);
        }
    }
}
