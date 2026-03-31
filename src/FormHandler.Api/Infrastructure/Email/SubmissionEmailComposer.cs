using System.Globalization;
using System.Reflection;
using System.Text;
using FormHandler.Api.Application.Email;
using FormHandler.Api.Application.Submissions;
using FormHandler.Api.Domain;
using FormHandler.Api.Domain.Fields;
using FormHandler.Api.Domain.Submissions;
using Mjml.Net;
using Scriban;
using Scriban.Runtime;

namespace FormHandler.Api.Infrastructure.Email
{
    public sealed class SubmissionEmailComposer : ISubmissionEmailComposer
    {
        private readonly ILogger<SubmissionEmailComposer> logger;

        private readonly string assemblyVersion;

        private readonly Lazy<Template> emailTemplate;

        private readonly MjmlRenderer mjmlRenderer = new();

        public SubmissionEmailComposer(ILogger<SubmissionEmailComposer> logger, IEmailTemplateProvider templateProvider)
        {
            this.logger = logger;
            this.assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version!.ToString(3);
            this.emailTemplate = new Lazy<Template>(() =>
            {
                var template = Template.Parse(templateProvider.GetTemplate("StandardEmailTemplate"));

                if (template.HasErrors)
                {
                    throw new InvalidOperationException(
                        string.Join("; ", template.Messages.Select(message => message.Message)));
                }

                return template;
            });
        }

        private (string Html, string? Error) BuildHtml(FormDefinition form, StoredFormSubmission submission)
        {
            var submissionValues = new ScriptObject();
            foreach (var field in form.Fields)
            {
                submissionValues[field.Name] = this.RenderFieldValue(field, submission);
            }

            var formFields = form.Fields.Select(field =>
            {
                var fieldObject = new ScriptObject();
                fieldObject["id"] = field.Id;
                fieldObject["name"] = field.Name;
                fieldObject["label"] = field.Label;
                return (object)fieldObject;
            }).ToList();

            var formObject = new ScriptObject();
            formObject["name"] = form.Name;
            formObject["fields"] = formFields;

            var brandingObject = new ScriptObject();
            brandingObject["primary_color"] = form.EmailOptions.Branding.PrimaryColor;
            brandingObject["secondary_color"] = form.EmailOptions.Branding.SecondaryColor;

            var submissionObject = new ScriptObject();
            submissionObject["values"] = submissionValues;
            submissionObject["submitted_at"] = submission.SubmittedAt.UtcDateTime.ToString("d MMMM yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC";

            var scriptObject = new ScriptObject();
            scriptObject["form"] = formObject;
            scriptObject["branding"] = brandingObject;
            scriptObject["submission"] = submissionObject;
            scriptObject["assembly_version"] = this.assemblyVersion;

            var context = new TemplateContext();
            context.PushGlobal(scriptObject);

            var mjml = this.emailTemplate.Value.Render(context);
            var rendered = this.mjmlRenderer.Render(mjml, new MjmlOptions());

            if (rendered.Errors.Count > 0)
            {
                return (string.Empty, string.Join("; ", rendered.Errors.Select(error => error.Error)));
            }

            return (rendered.Html, null);
        }

        private string BuildPlainText(FormDefinition form, StoredFormSubmission submission)
        {
            var builder = new StringBuilder();

            foreach (var field in form.Fields)
            {
                builder.AppendLine($"{field.Label}:");
                builder.AppendLine(this.RenderFieldValue(field, submission));
                builder.AppendLine();
            }

            return builder.ToString();
        }

        private string RenderFieldValue(FieldDefinition field, StoredFormSubmission submission)
        {
            if (!submission.Values.TryGetValue(field.Name, out var fieldValue))
            {
                return string.Empty;
            }

            return fieldValue switch
            {
                FieldValue.Text text => text.Value,
                FieldValue.Number number => number.Value.ToString(),
                FieldValue.Toggle toggle => toggle.Value ? "Yes" : "No",
                FieldValue.Selection selection when field is ChoiceFieldDefinition choice =>
                    string.Join(", ", selection.Values.Select(v =>
                        choice.Options.FirstOrDefault(o => o.Value == v)?.Label ?? v)),
                FieldValue.Selection selection => string.Join(", ", selection.Values),
                _ => string.Empty
            };
        }

        public EmailComposeResult Compose(FormDefinition form, StoredFormSubmission submission)
        {
            try
            {
                var (html, htmlError) = this.BuildHtml(form, submission);

                if (htmlError is not null)
                {
                    return new EmailComposeResult.Failed(htmlError);
                }

                string? replyTo = null;

                if (form.EmailOptions.ReplyToFieldName is not null)
                {
                    var replyToField = form.Fields.FirstOrDefault(field => field.Name == form.EmailOptions.ReplyToFieldName);

                    if (replyToField is null)
                    {
                        this.logger.LogWarning("Form {FormId}: ReplyToFieldName \"{ReplyToFieldName}\" does not match any field.", form.Id, form.EmailOptions.ReplyToFieldName);
                    }
                    else if (submission.Values.TryGetValue(replyToField.Name, out var replyToValue) && replyToValue is FieldValue.Text replyToText)
                    {
                        replyTo = replyToText.Value;
                    }
                }

                return new EmailComposeResult.Composed(new ComposedEmail(
                    $"New submission for form \"{form.Name}\"",
                    html,
                    this.BuildPlainText(form, submission),
                    replyTo));
            }
            catch (Exception exception)
            {
                return new EmailComposeResult.Failed(exception);
            }
        }
    }
}