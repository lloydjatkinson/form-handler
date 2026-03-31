using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using FormHandler.Api.Application.Submissions;
using FormHandler.Api.Domain.Submissions;
using Npgsql;
using NpgsqlTypes;

namespace FormHandler.Api.Infrastructure.Persistence
{
    public sealed class NpgsqlFormSubmissionStore : IFormSubmissionStore
    {
        private sealed record Metadata(
            [property: JsonPropertyName("ip_address")] string? IpAddress,
            [property: JsonPropertyName("forwarded_for")] string? ForwardedFor,
            [property: JsonPropertyName("user_agent")] string? UserAgent,
            [property: JsonPropertyName("accept_language")] string? AcceptLanguage,
            [property: JsonPropertyName("referer")] string? Referer,
            [property: JsonPropertyName("sec_ch_ua")] string? SecChUa,
            [property: JsonPropertyName("sec_ch_ua_platform")] string? SecChUaPlatform,
            [property: JsonPropertyName("sec_ch_ua_mobile")] string? SecChUaMobile);

        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            Converters = { new FieldValueConverter() }
        };

        private readonly DbDataSource dataSource;
        private readonly TimeProvider timeProvider;

        public NpgsqlFormSubmissionStore(DbDataSource dataSource, TimeProvider timeProvider)
        {
            this.dataSource = dataSource;
            this.timeProvider = timeProvider;
        }

        public async Task SaveAsync(FormSubmission submission, FormSubmissionMetadata metadata, CancellationToken cancellationToken = default)
        {
            await using var connection = await this.dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();

            command.CommandText = """
                WITH inserted AS (
                    INSERT INTO form_submissions (form_id, submitted_at, "values", metadata)
                    VALUES ($1, $2, $3, $4)
                    RETURNING id
                )
                INSERT INTO form_submissions_outbox (submission_id, type)
                SELECT id, t.type FROM inserted CROSS JOIN (VALUES ('email')) AS t(type)
                """;

            // SELECT id, t.type FROM inserted CROSS JOIN (VALUES ('email'), ('webhook')) AS t(type)

            using var valuesDocument = JsonSerializer.SerializeToDocument(submission.Values, jsonOptions);
            using var metadataDocument = JsonSerializer.SerializeToDocument(
                new Metadata(metadata.IpAddress, metadata.ForwardedFor, metadata.UserAgent,
                    metadata.AcceptLanguage, metadata.Referer, metadata.SecChUa,
                    metadata.SecChUaPlatform, metadata.SecChUaMobile));

            command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = submission.FormId });
            command.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { TypedValue = this.timeProvider.GetUtcNow() });
            command.Parameters.Add(new NpgsqlParameter { Value = valuesDocument, NpgsqlDbType = NpgsqlDbType.Jsonb });
            command.Parameters.Add(new NpgsqlParameter { Value = metadataDocument, NpgsqlDbType = NpgsqlDbType.Jsonb });

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<StoredFormSubmission?> GetSubmissionByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await using var connection = await this.dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();

            command.CommandText = """
                SELECT id, form_id, submitted_at, "values", metadata
                FROM form_submissions
                WHERE id = $1
                """;

            command.Parameters.Add(new NpgsqlParameter<Guid> { TypedValue = id });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            int idOrdinal = reader.GetOrdinal("id");
            int formIdOrdinal = reader.GetOrdinal("form_id");
            int submittedAtOrdinal = reader.GetOrdinal("submitted_at");
            int valuesOrdinal = reader.GetOrdinal("values");
            int metadataOrdinal = reader.GetOrdinal("metadata");

            var storedId = reader.GetFieldValue<Guid>(idOrdinal);
            string formId = reader.GetString(formIdOrdinal);
            var submittedAt = reader.GetFieldValue<DateTimeOffset>(submittedAtOrdinal);
            string valuesJson = reader.GetString(valuesOrdinal);
            string metadataJson = reader.GetString(metadataOrdinal);

            var values = JsonSerializer.Deserialize<Dictionary<string, FieldValue>>(valuesJson, jsonOptions)!;
            var storedMetadata = JsonSerializer.Deserialize<Metadata>(metadataJson)!;

            return new StoredFormSubmission
            {
                Id = storedId,
                FormId = formId,
                Values = values,
                SubmittedAt = submittedAt,
                Metadata = new FormSubmissionMetadata(storedMetadata.IpAddress, storedMetadata.ForwardedFor, storedMetadata.UserAgent, storedMetadata.AcceptLanguage, storedMetadata.Referer, storedMetadata.SecChUa, storedMetadata.SecChUaPlatform, storedMetadata.SecChUaMobile),
            };
        }

        public async Task<List<StoredFormSubmission>> GetSubmissionsAsync(string formId, CancellationToken cancellationToken = default)
        {
            await using var connection = await this.dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();

            command.CommandText = """
                SELECT id, submitted_at, "values", metadata
                FROM form_submissions
                WHERE form_id = $1
                ORDER BY submitted_at DESC
                """;

            command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = formId });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            int idOrdinal = reader.GetOrdinal("id");
            int submittedAtOrdinal = reader.GetOrdinal("submitted_at");
            int valuesOrdinal = reader.GetOrdinal("values");
            int metadataOrdinal = reader.GetOrdinal("metadata");

            var submissions = new List<StoredFormSubmission>();

            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetFieldValue<Guid>(idOrdinal);
                var submittedAt = reader.GetFieldValue<DateTimeOffset>(submittedAtOrdinal);
                var valuesJson = reader.GetString(valuesOrdinal);
                var metadataJson = reader.GetString(metadataOrdinal);

                var values = JsonSerializer.Deserialize<Dictionary<string, FieldValue>>(valuesJson, jsonOptions)!;
                var storedMetadata = JsonSerializer.Deserialize<Metadata>(metadataJson)!;

                submissions.Add(new StoredFormSubmission
                {
                    Id = id,
                    FormId = formId,
                    Values = values,
                    SubmittedAt = submittedAt,
                    Metadata = new FormSubmissionMetadata(storedMetadata.IpAddress, storedMetadata.ForwardedFor, storedMetadata.UserAgent, storedMetadata.AcceptLanguage, storedMetadata.Referer, storedMetadata.SecChUa, storedMetadata.SecChUaPlatform, storedMetadata.SecChUaMobile),
                });
            }

            return submissions;
        }
    }
}