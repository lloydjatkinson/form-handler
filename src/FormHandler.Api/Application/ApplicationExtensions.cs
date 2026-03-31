using FormHandler.Api.Application.Outbox;
using FormHandler.Api.Domain.Submissions;

namespace FormHandler.Api.Application
{
    public static class ApplicationExtensions
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IFormRegistry, FormRegistry>();
            services.AddSingleton<FormSubmissionValidator>();
            services.AddHostedService<OutboxProcessor>();
            return services;
        }
    }
}