using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FormHandler.Api.Infrastructure.HealthChecks
{
    public sealed class PostgresConnectionCheck : IHealthCheck
    {
        private readonly DbDataSource dataSource;

        public PostgresConnectionCheck(DbDataSource dataSource)
        {
            this.dataSource = dataSource;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = await this.dataSource.OpenConnectionAsync(cancellationToken);

                var connectionStats = await connection.QuerySingleAsync(
                    "SELECT count(*)::int AS connections, (SELECT setting::int FROM pg_settings WHERE name = 'max_connections') AS max_connections FROM pg_stat_activity");
                int activeConnections = (int)connectionStats.connections;
                int maxConnections = (int)connectionStats.max_connections;
                double connectionRatio = (double)activeConnections / maxConnections;

                if (connectionRatio >= 0.95) return HealthCheckResult.Unhealthy($"Connection count critical: {activeConnections}/{maxConnections} ({connectionRatio:P0} of max).");
                if (connectionRatio >= 0.80) return HealthCheckResult.Degraded($"Connection count high: {activeConnections}/{maxConnections} ({connectionRatio:P0} of max).");

                return HealthCheckResult.Healthy($"Connection count normal: {activeConnections}/{maxConnections} ({connectionRatio:P0} of max).");
            }
            catch (Exception exception)
            {
                return HealthCheckResult.Degraded(exception.Message, exception);
            }
        }
    }
}