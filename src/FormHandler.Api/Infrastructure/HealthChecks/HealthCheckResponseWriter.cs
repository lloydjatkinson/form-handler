using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FormHandler.Api.Infrastructure.HealthChecks
{
    public static class HealthCheckResponseWriter
    {
        private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true, IndentSize = 4 };

        public static Task WriteResponse(HttpContext httpContext, HealthReport report)
        {
            var payload = new
            {
                status = report.Status.ToString(),
                totalDuration = report.TotalDuration,
                checks = report.Entries.Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    duration = entry.Value.Duration,
                })
            };

            return httpContext.Response.WriteAsJsonAsync(payload, jsonOptions);
        }
    }
}