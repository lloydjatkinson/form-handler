using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FormHandler.Api.Application.Submissions;
using FormHandler.Api.Domain.Submissions;
using FormHandler.Api.Infrastructure.Persistence;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace FormHandler.Tests.Infrastructure.Persistence
{
    [Collection("Postgres")]
    public sealed class NpgsqlFormSubmissionStoreTests : IAsyncLifetime
    {
        private readonly PostgresContainerFixture fixture;
        private readonly NpgsqlFormSubmissionStore store;

        public NpgsqlFormSubmissionStoreTests(PostgresContainerFixture fixture)
        {
            this.fixture = fixture;
            this.store = new NpgsqlFormSubmissionStore(fixture.DataSource, TimeProvider.System);
        }

        public async ValueTask InitializeAsync()
        {
            await using var connection = await this.fixture.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "TRUNCATE form_submissions CASCADE";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

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
        public async Task QueryResultsExcludeOtherForms(FormSubmission submissionA, FormSubmission submissionB, FormSubmissionMetadata metadataA, FormSubmissionMetadata metadataB)
        {
            await this.store.SaveAsync(submissionA, metadataA, TestContext.Current.CancellationToken);
            await this.store.SaveAsync(submissionB, metadataB, TestContext.Current.CancellationToken);

            var results = await this.store.GetSubmissionsAsync(submissionA.FormId, TestContext.Current.CancellationToken);

            results.ShouldHaveSingleItem().FormId.ShouldBe(submissionA.FormId);
        }

        [Theory, FormHandlerAutoData]
        public async Task AllFieldValueTypesRoundTripCorrectly(FormSubmissionMetadata metadata)
        {
            var values = new Dictionary<string, FieldValue>
            {
                ["name"] = new FieldValue.Text("Alice"),
                ["age"] = new FieldValue.Number(30),
                ["newsletter"] = new FieldValue.Toggle(true),
                ["interests"] = new FieldValue.Selection(["coding", "gaming"])
            };

            await this.store.SaveAsync(new FormSubmission { FormId = "test-form", Values = values }, metadata, TestContext.Current.CancellationToken);

            var result = (await this.store.GetSubmissionsAsync("test-form", TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
            result.Values["name"].ShouldBe(new FieldValue.Text("Alice"));
            result.Values["age"].ShouldBe(new FieldValue.Number(30));
            result.Values["newsletter"].ShouldBe(new FieldValue.Toggle(true));
            result.Values["interests"].ShouldBeOfType<FieldValue.Selection>().Values.ShouldBe(["coding", "gaming"]);
        }

        [Theory, FormHandlerAutoData]
        public async Task SubmissionMetadataRoundTripsCorrectly(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.store.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);

            var result = (await this.store.GetSubmissionsAsync(submission.FormId, TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
            result.Metadata.ShouldBe(metadata);
        }

        [Theory, FormHandlerAutoData]
        public async Task SubmissionsAreReturnedNewestFirst(FormSubmission submission, FormSubmissionMetadata metadata1, FormSubmissionMetadata metadata2)
        {
            var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var localStore = new NpgsqlFormSubmissionStore(this.fixture.DataSource, timeProvider);

            await localStore.SaveAsync(submission, metadata1, TestContext.Current.CancellationToken);
            timeProvider.Advance(TimeSpan.FromHours(1));
            await localStore.SaveAsync(submission, metadata2, TestContext.Current.CancellationToken);

            var results = await localStore.GetSubmissionsAsync(submission.FormId, TestContext.Current.CancellationToken);

            results.Count.ShouldBe(2);
            results[0].SubmittedAt.ShouldBeGreaterThan(results[1].SubmittedAt);
        }

        [Fact]
        public async Task UnknownSubmissionIdReturnsNull()
        {
            var result = await this.store.GetSubmissionByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

            result.ShouldBeNull();
        }

        [Theory, FormHandlerAutoData]
        public async Task SubmissionCanBeRetrievedById(FormSubmissionMetadata metadata)
        {
            await this.store.SaveAsync(new FormSubmission { FormId = "test-form", Values = new Dictionary<string, FieldValue> { ["name"] = new FieldValue.Text("Alice") } }, metadata, TestContext.Current.CancellationToken);

            var saved = (await this.store.GetSubmissionsAsync("test-form", TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
            var result = await this.store.GetSubmissionByIdAsync(saved.Id, TestContext.Current.CancellationToken);

            result.ShouldNotBeNull();
            result.Id.ShouldBe(saved.Id);
            result.FormId.ShouldBe("test-form");
            result.Values["name"].ShouldBe(new FieldValue.Text("Alice"));
        }

        [Theory, FormHandlerAutoData]
        public async Task GetByIdDoesNotReturnSubmissionWithDifferentId(FormSubmission submission, FormSubmissionMetadata metadata)
        {
            await this.store.SaveAsync(submission, metadata, TestContext.Current.CancellationToken);

            var result = await this.store.GetSubmissionByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

            result.ShouldBeNull();
        }
    }
}