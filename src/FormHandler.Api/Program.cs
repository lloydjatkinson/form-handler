using System.Text.Json;
using System.Threading.RateLimiting;
using FormHandler.Api.Application;
using FormHandler.Api.Application.Forms;
using FormHandler.Api.Endpoints;
using FormHandler.Api.Infrastructure;
using FormHandler.Api.Infrastructure.HealthChecks;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;

namespace FormHandler.Api
{
    public class Program
    {
        //private static 

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateBootstrapLogger();

            try
            {
                var builder = WebApplication.CreateBuilder(args);

                builder.Services.AddSerilog((services, loggerConfiguration) =>
                {
                    loggerConfiguration
                        .MinimumLevel.Information()
                        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                        .MinimumLevel.Override("Npgsql", LogEventLevel.Warning)
                        .Enrich.FromLogContext()
                        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");

                    loggerConfiguration.WriteTo.File(
                        "logs/app.log",
                        rollingInterval: RollingInterval.Day,
                        retainedFileTimeLimit: TimeSpan.FromDays(60),
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
                });

                builder.Services.AddRateLimiter(options =>
                {
                    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "None",
                            factory: partition => new FixedWindowRateLimiterOptions
                            {
                                AutoReplenishment = true,
                                PermitLimit = 20,
                                QueueLimit = 0,
                                Window = TimeSpan.FromMinutes(2),
                            }));

                    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                });

                builder.Services.AddOpenApi();
                builder.Services.AddProblemDetails();

                builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
                builder.Services.AddApplication();

                var app = builder.Build();

                if (!string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Default")))
                {
                    using var scope = app.Services.CreateScope();
                    scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
                }

                if (app.Environment.IsDevelopment())
                {
                    app.MapOpenApi();
                }

                //app.UseRateLimiter();

                app.UseExceptionHandler(exceptionHandlerApp => exceptionHandlerApp.Run(async httpContext =>
                {
                    var exception = httpContext.Features.Get<IExceptionHandlerFeature>()?.Error;
                    var statusCode = exception switch
                    {
                        JsonException => StatusCodes.Status400BadRequest,
                        BadHttpRequestException badHttpRequestException => badHttpRequestException.StatusCode,
                        _ => StatusCodes.Status500InternalServerError
                    };
                    httpContext.Response.StatusCode = statusCode;
                    await Results.Problem(statusCode: statusCode).ExecuteAsync(httpContext);
                }));

                if (app.Environment.IsDevelopment())
                {
                    app.UseHttpsRedirection();
                }

                app.UseSerilogRequestLogging(options =>
                {
                    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                    {
                        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
                    };
                });

                app.MapForms();

                app.MapHealthChecks("/health", new HealthCheckOptions
                {
                    ResponseWriter = HealthCheckResponseWriter.WriteResponse,
                });

                app.Run();
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, "Application terminated unexpectedly");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}