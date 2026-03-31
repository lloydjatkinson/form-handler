using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FormHandler.Api.Infrastructure.HealthChecks
{
    public sealed class PostgresDeadlockCheck : IHealthCheck
    {
        private readonly DbDataSource dataSource;

        public PostgresDeadlockCheck(DbDataSource dataSource)
        {
            this.dataSource = dataSource;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = await this.dataSource.OpenConnectionAsync(cancellationToken);

                int deadlockCount = await connection.ExecuteScalarAsync<int>(
                    "SELECT deadlocks FROM pg_stat_database WHERE datname = current_database()");
                if (deadlockCount > 0) return HealthCheckResult.Degraded($"Database has recorded {deadlockCount} deadlock(s).");

                return HealthCheckResult.Healthy("No deadlocks recorded.");
            }
            catch (Exception exception)
            {
                return HealthCheckResult.Degraded(exception.Message, exception);
            }
        }
    }
}