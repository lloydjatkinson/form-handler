using System.Collections.Concurrent;
using FormHandler.Api.Application.Outbox;
using FormHandler.Api.Application.Submissions;
using FormHandler.Api.Domain.Submissions;

namespace FormHandler.Api.Infrastructure.Persistence
{
    public sealed class InMemoryFormSubmissionStore : IFormSubmissionStore, IFormSubmissionOutboxStore
    {
        private readonly ConcurrentBag<StoredFormSubmission> submissions = [];
        private readonly ConcurrentDictionary<Guid, InMemoryOutboxEntry> outboxEntries = new();
        private readonly TimeProvider timeProvider;

        public InMemoryFormSubmissionStore(TimeProvider timeProvider)
        {
            this.timeProvider = timeProvider;
        }

        public Task SaveAsync(FormSubmission submission, FormSubmissionMetadata metadata, CancellationToken cancellationToken = default)
        {
            var submissionId = Guid.NewGuid();

            this.submissions.Add(new StoredFormSubmission
            {
                Id = submissionId,
                FormId = submission.FormId,
                Values = submission.Values,
                SubmittedAt = this.timeProvider.GetUtcNow(),
                Metadata = metadata,
            });

            this.outboxEntries[Guid.NewGuid()] = new InMemoryOutboxEntry
            {
                SubmissionId = submissionId,
                Type = "email"
            };

            this.outboxEntries[Guid.NewGuid()] = new InMemoryOutboxEntry
            {
                SubmissionId = submissionId,
                Type = "webhook"
            };

            return Task.CompletedTask;
        }

        public Task<StoredFormSubmission?> GetSubmissionByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.submissions.FirstOrDefault(s => s.Id == id));
        }

        public Task<List<StoredFormSubmission>> GetSubmissionsAsync(string formId, CancellationToken cancellationToken = default)
        {
            var results = new List<StoredFormSubmission>();

            foreach (var submission in this.submissions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (submission.FormId == formId)
                {
                    results.Add(submission);
                }
            }

            return Task.FromResult(results);
        }

        public Task<IReadOnlyCollection<PendingOutboxEntry>> ClaimPendingAsync(CancellationToken cancellationToken = default)
        {
            var now = this.timeProvider.GetUtcNow();
            var entries = new List<PendingOutboxEntry>();

            foreach (var (id, entry) in this.outboxEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.ProcessedAt is not null || entry.FailedAt is not null)
                    continue;

                if (entry.LastAttemptedAt is not null &&
                    entry.LastAttemptedAt.Value >= now - TimeSpan.FromMinutes(Math.Pow(2, entry.AttemptCount)))
                    continue;

                entry.AttemptCount++;
                entry.LastAttemptedAt = now;
                entry.LastError = null;

                entries.Add(new PendingOutboxEntry(id, entry.SubmissionId, entry.Type, entry.AttemptCount));
            }

            return Task.FromResult<IReadOnlyCollection<PendingOutboxEntry>>(entries);
        }

        public Task MarkAsSentAsync(Guid outboxId, CancellationToken cancellationToken = default)
        {
            if (this.outboxEntries.TryGetValue(outboxId, out var entry))
            {
                entry.ProcessedAt = this.timeProvider.GetUtcNow();
            }

            return Task.CompletedTask;
        }

        public Task<OutboxFailureResult> MarkAsFailedAsync(Guid outboxId, string error, CancellationToken cancellationToken = default)
        {
            if (this.outboxEntries.TryGetValue(outboxId, out var entry))
            {
                entry.LastError = error;

                if (entry.AttemptCount >= 5)
                {
                    entry.FailedAt = this.timeProvider.GetUtcNow();
                    return Task.FromResult<OutboxFailureResult>(new OutboxFailureResult.RetriesExhausted());
                }

                var retryIn = TimeSpan.FromMinutes(Math.Pow(2, entry.AttemptCount));
                return Task.FromResult<OutboxFailureResult>(new OutboxFailureResult.WillRetry(retryIn));
            }

            return Task.FromResult<OutboxFailureResult>(new OutboxFailureResult.WillRetry(TimeSpan.Zero));
        }

        private sealed class InMemoryOutboxEntry
        {
            public required Guid SubmissionId { get; init; }
            public required string Type { get; init; }
            public int AttemptCount;
            public DateTimeOffset? LastAttemptedAt;
            public string? LastError;
            public DateTimeOffset? ProcessedAt;
            public DateTimeOffset? FailedAt;
        }
    }
}
