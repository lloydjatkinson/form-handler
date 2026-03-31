using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FormHandler.Api.Infrastructure.HealthChecks
{
    public sealed class PostgresConnectivityCheck : IHealthCheck
    {
        private readonly DbDataSource dataSource;

        public PostgresConnectivityCheck(DbDataSource dataSource)
        {
            this.dataSource = dataSource;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = await this.dataSource.OpenConnectionAsync(cancellationToken);

                bool hasConnection = await connection.ExecuteScalarAsync<int>("SELECT 1") == 1;
                if (!hasConnection) return HealthCheckResult.Unhealthy("PostgreSQL connection failed.");

                bool hasFormSubmissionsTable = await connection.ExecuteScalarAsync<bool>(
                    "SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'form_submissions')");

                bool hasFormSubmissionOutboxTable = await connection.ExecuteScalarAsync<bool>(
                    "SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'form_submissions_outbox')");

                bool hasSchema = hasFormSubmissionsTable && hasFormSubmissionOutboxTable;
                if (!hasSchema) return HealthCheckResult.Unhealthy("One or more required tables are missing.");

                return HealthCheckResult.Healthy("PostgreSQL is healthy.");
            }
            catch (Exception exception)
            {
                return HealthCheckResult.Unhealthy(exception.Message, exception);
            }
        }
    }
}