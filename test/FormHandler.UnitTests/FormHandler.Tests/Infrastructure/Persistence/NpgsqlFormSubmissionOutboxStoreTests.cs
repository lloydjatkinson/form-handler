using System;
using System.Threading.Tasks;
using FormHandler.Api.Application.Outbox;
using FormHandler.Api.Application.Submissions;
using FormHandler.Api.Domain.Submissions;
using FormHandler.Api.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Npgsql;
using Shouldly;

namespace FormHandler.Tests.Infrastructure.Persistence
{
    [Collection("Postgres")]
    public sealed class NpgsqlFormSubmissionOutboxStoreTests : IAsyncLifetime
    {
        private readonly PostgresContainerFixture fixture;
        private readonly NpgsqlFormSubmissionOutboxStore store;
        private readonly NpgsqlFormSubmissionStore submissionStore;

        public NpgsqlFormSubmissionOutboxStoreTests(PostgresContainerFixture fixture)
        {
            this.fixture = fixture;
            this.store = new NpgsqlFormSubmissionOutboxStore(fixture.DataSource, TimeProvider.System, Options.Create(new OutboxOptions()));
            this.submissionStore = new NpgsqlFormSubmissionStore(fixture.DataSource, TimeProvider.System);
        }

        public async ValueTask InitializeAsync()
        {
            await using var connection = await this.fixture.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "TRUNCATE form_submissions CASCADE";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private async Task BackdateOutboxEntryAsync(Guid outboxId, int attemptCount)
        {
            await using var connection = await this.fixture.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE form_submissions_outbox
                SET attempt_count = $2, last_attempted_at = now() - interval '1000 minutes'
                WHERE id = $1
                """;
            command.Parameters.Add(new NpgsqlParameter<Guid> { TypedValue = outboxId });
            command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = attemptCount });
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        [Theory, FormHandlerAutoData]
        public async Task BatchSizeLimitsClaimedEntries(
            FormSubmission submission1, FormSubmissionMetadata metadata1,
            FormSubmission submission2, FormSubmissionMetadata metadata2,
            FormSubmission submission3, FormSubmissionMetadata metadata3)
        {
            await this.submissionStore.SaveAsync(submission1, metadata1, TestContext.Current.CancellationToken);
            await this.submissionStore.SaveAsync(submission2, metadata2, TestContext.Current.CancellationToken);
            await this.submissionStore.SaveAsync(submission3, metadata3, TestContext.Current.CancellationToken);

            var limitedStore = new NpgsqlFormSubmissionOutboxStore(this.fixture.DataSource, TimeProvider.System, Options.Create(new OutboxOptions { BatchSize = 2 }));
            var entries = await limitedStore.ClaimPendingAsync(TestContext.Current.CancellationToken);

            entries.Count.ShouldBe(2);
        }

        [Fact]
        public async Task NoEntriesReturnedFromEmptyOutbox()
        {
            var entries = await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken);

            entries.ShouldBeEmpty();
        }

        [Theory, FormHandlerAutoData]
        public async Task NewSubmissionProducesClaimableEntry(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.submissionStore.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);

            var entries = await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken);

            entries.ShouldHaveSingleItem();
        }

        [Theory, FormHandlerAutoData]
        public async Task ClaimingAnEntryIncrementsAttemptCount(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.submissionStore.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);

            var entry = (await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();

            entry.AttemptCount.ShouldBe(1);
        }

        [Theory, FormHandlerAutoData]
        public async Task RecentlyClaimedEntryIsWithheldDuringBackoff(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.submissionStore.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);
            await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken);

            var entries = await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken);

            entries.ShouldBeEmpty();
        }

        [Theory, FormHandlerAutoData]
        public async Task DeadLetteredEntryIsNeverClaimedAgain(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.submissionStore.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);
            var entry = (await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
            await this.BackdateOutboxEntryAsync(entry.OutboxId, 4);
            var retriedEntry = (await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
            await this.store.MarkAsFailedAsync(retriedEntry.OutboxId, "error", TestContext.Current.CancellationToken);

            var entries = await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken);

            entries.ShouldBeEmpty();
        }

        [Theory, FormHandlerAutoData]
        public async Task ProcessedEntryIsNeverClaimedAgain(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.submissionStore.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);
            var entry = (await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();

            await this.store.MarkAsSentAsync(entry.OutboxId, TestContext.Current.CancellationToken);

            var entries = await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken);
            entries.ShouldBeEmpty();
        }

        [Theory, FormHandlerAutoData]
        public async Task FailedEntryWithRemainingAttemptsSchedulesRetry(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.submissionStore.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);
            var entry = (await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();

            var result = await this.store.MarkAsFailedAsync(entry.OutboxId, "test error", TestContext.Current.CancellationToken);

            result.ShouldBeOfType<OutboxFailureResult.WillRetry>().RetryIn.ShouldBeInRange(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(3));
        }

        [Theory, FormHandlerAutoData]
        public async Task RetryDelayDoublesWithEachFailure(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.submissionStore.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);
            var outboxId = (await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken)).ShouldHaveSingleItem().OutboxId;

            var retryIn1 = (await this.store.MarkAsFailedAsync(outboxId, "error", TestContext.Current.CancellationToken)).ShouldBeOfType<OutboxFailureResult.WillRetry>().RetryIn;
            await this.BackdateOutboxEntryAsync(outboxId, 1);
            await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken);

            var retryIn2 = (await this.store.MarkAsFailedAsync(outboxId, "error", TestContext.Current.CancellationToken)).ShouldBeOfType<OutboxFailureResult.WillRetry>().RetryIn;
            await this.BackdateOutboxEntryAsync(outboxId, 2);
            await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken);

            var retryIn3 = (await this.store.MarkAsFailedAsync(outboxId, "error", TestContext.Current.CancellationToken)).ShouldBeOfType<OutboxFailureResult.WillRetry>().RetryIn;

            retryIn1.ShouldBeInRange(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(3));
            retryIn2.ShouldBeInRange(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(5));
            retryIn3.ShouldBeInRange(TimeSpan.FromMinutes(7), TimeSpan.FromMinutes(9));
        }

        [Theory, FormHandlerAutoData]
        public async Task EntryIsDeadLetteredAfterFiveFailures(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.submissionStore.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);
            var entry = (await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
            await this.BackdateOutboxEntryAsync(entry.OutboxId, 4);
            var retriedEntry = (await this.store.ClaimPendingAsync(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();

            var result = await this.store.MarkAsFailedAsync(retriedEntry.OutboxId, "fatal error", TestContext.Current.CancellationToken);

            result.ShouldBeOfType<OutboxFailureResult.RetriesExhausted>();
        }
    }
}