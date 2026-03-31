using System.Data.Common;
using FormHandler.Api.Application.Outbox;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FormHandler.Api.Infrastructure.Persistence
{
    public sealed class NpgsqlFormSubmissionOutboxStore : IFormSubmissionOutboxStore
    {
        private readonly DbDataSource dataSource;
        private readonly TimeProvider timeProvider;
        private readonly OutboxOptions options;

        public NpgsqlFormSubmissionOutboxStore(DbDataSource dataSource, TimeProvider timeProvider, IOptions<OutboxOptions> options)
        {
            this.dataSource = dataSource;
            this.timeProvider = timeProvider;
            this.options = options.Value;
        }

        public async Task<IReadOnlyCollection<PendingOutboxEntry>> ClaimPendingAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = await this.dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();

            command.CommandText = """
                WITH claimed AS (
                    SELECT id FROM form_submissions_outbox
                    WHERE processed_at IS NULL AND failed_at IS NULL
                      AND (last_attempted_at IS NULL
                           OR last_attempted_at < now() - (power(2, attempt_count) * interval '1 minute'))
                    ORDER BY created_at
                    LIMIT $1
                    FOR UPDATE SKIP LOCKED
                )
                UPDATE form_submissions_outbox
                SET attempt_count = attempt_count + 1,
                    last_attempted_at = now(),
                    last_error = NULL
                FROM claimed
                WHERE form_submissions_outbox.id = claimed.id
                RETURNING form_submissions_outbox.id, submission_id, type, attempt_count
                """;

            command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = this.options.BatchSize });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            int idOrdinal = reader.GetOrdinal("id");
            int submissionIdOrdinal = reader.GetOrdinal("submission_id");
            int typeOrdinal = reader.GetOrdinal("type");
            int attemptCountOrdinal = reader.GetOrdinal("attempt_count");

            var entries = new List<PendingOutboxEntry>();

            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(new PendingOutboxEntry(
                    reader.GetFieldValue<Guid>(idOrdinal),
                    reader.GetFieldValue<Guid>(submissionIdOrdinal),
                    reader.GetString(typeOrdinal),
                    reader.GetInt32(attemptCountOrdinal)));
            }

            return entries;
        }

        public async Task MarkAsSentAsync(Guid outboxId, CancellationToken cancellationToken = default)
        {
            await using var connection = await this.dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();

            command.CommandText = """
                UPDATE form_submissions_outbox
                SET processed_at = now()
                WHERE id = $1
                """;

            command.Parameters.Add(new NpgsqlParameter<Guid> { TypedValue = outboxId });

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<OutboxFailureResult> MarkAsFailedAsync(Guid outboxId, string error, CancellationToken cancellationToken = default)
        {
            await using var connection = await this.dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();

            command.CommandText = """
                UPDATE form_submissions_outbox
                SET last_error = $2,
                    failed_at = CASE WHEN attempt_count >= 5 THEN now() ELSE NULL END
                WHERE id = $1
                RETURNING CASE WHEN attempt_count >= 5 THEN NULL
                               ELSE last_attempted_at + (power(2, attempt_count) * interval '1 minute')
                          END AS retry_after
                """;

            command.Parameters.Add(new NpgsqlParameter<Guid> { TypedValue = outboxId });
            command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = error });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);

            int retryAfterOrdinal = reader.GetOrdinal("retry_after");

            if (await reader.IsDBNullAsync(retryAfterOrdinal, cancellationToken))
            {
                return new OutboxFailureResult.RetriesExhausted();
            }

            var retryAfter = reader.GetFieldValue<DateTimeOffset>(retryAfterOrdinal);
            return new OutboxFailureResult.WillRetry(retryAfter - this.timeProvider.GetUtcNow());
        }
    }
}