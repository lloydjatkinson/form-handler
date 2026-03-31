using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FormHandler.Api.Infrastructure.HealthChecks
{
    public class LoggingHealthCheckPublisher : IHealthCheckPublisher
    {
        private readonly ILogger<LoggingHealthCheckPublisher> logger;

        public LoggingHealthCheckPublisher(ILogger<LoggingHealthCheckPublisher> logger)
        {
            this.logger = logger;
        }

        public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
        {
            if (report.Status == HealthStatus.Healthy) return Task.CompletedTask;

            var level = report.Status switch
            {
                HealthStatus.Healthy => LogLevel.Information,
                HealthStatus.Degraded => LogLevel.Warning,
                HealthStatus.Unhealthy => LogLevel.Error,
                _ => LogLevel.Critical
            };

            string degradedOrUnhealthy = string.Join(", ", report.Entries
                .Where(entry => entry.Value.Status != HealthStatus.Healthy)
                .Select(entry => $"{entry.Key}: {entry.Value.Status}"));

            this.logger.Log(level, "Health check report: {Status}. Affected checks: {Checks}", report.Status, degradedOrUnhealthy);

            return Task.CompletedTask;
        }
    }
}