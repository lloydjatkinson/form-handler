using FormHandler.Api.Application.Email;
using FormHandler.Api.Application.Forms;
using FormHandler.Api.Application.Outbox;
using FormHandler.Api.Application.Submissions;
using FormHandler.Api.Infrastructure.Email;
using FormHandler.Api.Infrastructure.Forms;
using FormHandler.Api.Infrastructure.HealthChecks;
using FormHandler.Api.Infrastructure.Persistence;
using FluentMigrator.Runner;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

namespace FormHandler.Api.Infrastructure
{
    public static class InfrastructureExtensions
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            services.AddOptions<EmailOptions>()
                .BindConfiguration("Email")
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IEmailTemplateProvider, EmbeddedResourceEmailTemplateProvider>();
            services.AddSingleton<ISubmissionEmailComposer, SubmissionEmailComposer>();
            services.AddSingleton<IEmailSender, MailKitEmailSender>();

            services.AddSingleton<IFormDefinitionStore>(LoadForms(configuration, environment));

            string? connectionString = configuration.GetConnectionString("Default");
            var healthChecks = services.AddHealthChecks();

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                services.AddNpgsqlDataSource(connectionString);
                services.AddScoped<IFormSubmissionStore, NpgsqlFormSubmissionStore>();
                services.AddScoped<IFormSubmissionOutboxStore, NpgsqlFormSubmissionOutboxStore>();
                services.AddFluentMigratorCore()
                    .ConfigureRunner(runner => runner
                        .AddPostgres()
                        .WithGlobalConnectionString(connectionString)
                        .ScanIn(typeof(InfrastructureExtensions).Assembly).For.Migrations());

                services.AddSingleton<IHealthCheckPublisher, LoggingHealthCheckPublisher>();
                healthChecks.AddCheck<PostgresConnectivityCheck>(nameof(PostgresConnectivityCheck));
                healthChecks.AddCheck<PostgresConnectionCheck>(nameof(PostgresConnectionCheck));
                healthChecks.AddCheck<PostgresActivityCheck>(nameof(PostgresActivityCheck));
                healthChecks.AddCheck<PostgresDeadlockCheck>(nameof(PostgresDeadlockCheck));
                healthChecks.AddCheck<OutboxHealthCheck>(nameof(OutboxHealthCheck));
            }
            else
            {
                services.AddSingleton<IFormSubmissionStore, InMemoryFormSubmissionStore>();
                services.AddSingleton<IFormSubmissionOutboxStore, InMemoryFormSubmissionStore>();
            }

            return services;
        }

        private static InMemoryFormDefinitionStore LoadForms(IConfiguration configuration, IHostEnvironment environment)
        {
            var formsDirectory = configuration["FormDefinitions:Directory"]
                ?? throw new InvalidOperationException("\"FormDefinitions:Directory\" is not configured.");
            var formsDir = Path.IsPathRooted(formsDirectory)
                ? formsDirectory
                : Path.Combine(environment.ContentRootPath, formsDirectory);

            var forms = new YamlFormDefinitionLoader().Load(formsDir);

            Log.Information("Loaded {Count} form(s) from {Directory}", forms.Count, formsDir);

            return new InMemoryFormDefinitionStore(forms);
        }
    }
}
