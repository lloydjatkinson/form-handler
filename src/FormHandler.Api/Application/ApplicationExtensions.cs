using FormHandler.Api.Application.Outbox;
using FormHandler.Api.Domain.Submissions;

namespace FormHandler.Api.Application
{
    public static class ApplicationExtensions
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddOptions<OutboxOptions>()
                .BindConfiguration("Outbox")
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<FormSubmissionValidator>();
            services.AddHostedService<OutboxProcessor>();
            return services;
        }
    }
}
