using System.Data.Common;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FormHandler.Api.Infrastructure.HealthChecks
{
    public sealed class OutboxHealthCheck : IHealthCheck
    {
        private readonly DbDataSource dataSource;

        public OutboxHealthCheck(DbDataSource dataSource)
        {
            this.dataSource = dataSource;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = await this.dataSource.OpenConnectionAsync(cancellationToken);

                var statistics = await connection.QuerySingleAsync<OutboxStatistics>("""
                    SELECT
                        (COUNT(*) FILTER (WHERE processed_at IS NULL AND failed_at IS NULL AND attempt_count = 0))::int AS queued,
                        (COUNT(*) FILTER (WHERE processed_at IS NULL AND failed_at IS NULL AND attempt_count > 0))::int AS attempting,
                        (COUNT(*) FILTER (WHERE failed_at IS NOT NULL))::int AS deadlettered,
                        MIN(created_at) FILTER (WHERE processed_at IS NULL AND failed_at IS NULL) AS oldestpendingat
                    FROM form_submissions_outbox
                    """);

                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(statistics))!;
                var summary = $"Queued: {statistics.Queued}, attempting: {statistics.Attempting}, dead lettered: {statistics.DeadLettered}.";

                return statistics switch
                {
                    { DeadLettered: > 0 } => HealthCheckResult.Degraded(summary, data: data),
                    _ => HealthCheckResult.Healthy(summary, data: data),
                };
            }
            catch (Exception exception)
            {
                return HealthCheckResult.Degraded(exception.Message, exception);
            }
        }

        private sealed record OutboxStatistics(
            int Queued,
            int Attempting,
            int DeadLettered,
            DateTime? OldestPendingAt);
    }
}