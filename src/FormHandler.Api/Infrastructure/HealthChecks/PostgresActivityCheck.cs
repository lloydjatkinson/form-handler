using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FormHandler.Api.Infrastructure.HealthChecks
{
    public sealed class PostgresActivityCheck : IHealthCheck
    {
        private readonly DbDataSource dataSource;

        public PostgresActivityCheck(DbDataSource dataSource)
        {
            this.dataSource = dataSource;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = await this.dataSource.OpenConnectionAsync(cancellationToken);

                int idleInTransactionCount = await connection.ExecuteScalarAsync<int>(
                    "SELECT count(*)::int FROM pg_stat_activity WHERE state = 'idle in transaction' AND state_change < now() - interval '5 minutes'");
                if (idleInTransactionCount > 0) return HealthCheckResult.Degraded($"{idleInTransactionCount} connection(s) idle in transaction for more than 5 minutes.");

                int longRunningTransactionCount = await connection.ExecuteScalarAsync<int>(
                    "SELECT count(*)::int FROM pg_stat_activity WHERE xact_start IS NOT NULL AND xact_start < now() - interval '1 hour'");
                if (longRunningTransactionCount > 0) return HealthCheckResult.Degraded($"{longRunningTransactionCount} transaction(s) running for more than 1 hour.");

                return HealthCheckResult.Healthy("No problematic activity detected.");
            }
            catch (Exception exception)
            {
                return HealthCheckResult.Degraded(exception.Message, exception);
            }
        }
    }
}