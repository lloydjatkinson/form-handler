using FormHandler.Api.Application.Email;

namespace FormHandler.Api.Infrastructure.Email
{
    public sealed class EmbeddedResourceEmailTemplateProvider : IEmailTemplateProvider
    {
        public string GetTemplate(string name)
        {
            var assembly = typeof(EmbeddedResourceEmailTemplateProvider).Assembly;
            using var stream = assembly.GetManifestResourceStream($"FormHandler.Api.Templates.{name}.mjml.scriban")!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}