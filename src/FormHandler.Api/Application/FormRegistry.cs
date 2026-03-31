using System.Collections.Frozen;
using FormHandler.Api.Domain;
using FormHandler.Api.Domain.Fields;

namespace FormHandler.Api.Application
{
    public sealed class FormRegistry : IFormRegistry
    {
        private static readonly FrozenDictionary<string, FormDefinition> forms = new[]
        {
            new FormDefinition
            {
                Id = "inbox",
                Name = "Inbox",
                EmailOptions = new FormSubmissionEmailOptions { To = "inbox@lloydatkinson.net", ReplyToFieldName = "email", Branding = new FormSubmissionEmailBranding { PrimaryColor = "#ff0000" } },
                Fields =
                [
                    new TextFieldDefinition { Id = "first-name", Name = "firstName", Label = "First Name", IsRequired = true },
                    new TextFieldDefinition { Id = "last-name", Name = "lastName", Label = "Last Name", IsRequired = true },
                    new TextFieldDefinition { Id = "company", Name = "company", Label = "Company" },
                    new EmailFieldDefinition { Id = "email", Name = "email", Label = "Email Address", IsRequired = true },
                    new TextAreaFieldDefinition { Id = "message", Name = "message", Label = "Message", IsRequired = true }
                ]
            },
            new FormDefinition
            {
                Id = "survey",
                Name = "Survey",
                EmailOptions = new FormSubmissionEmailOptions { To = "lloydjatkinson@gmail.com", ReplyToFieldName = "email" },
                Fields =
                [
                    new TextFieldDefinition { Id = "full-name", Name = "fullName", Label = "Full Name", IsRequired = true },
                    new EmailFieldDefinition { Id = "email", Name = "email", Label = "Email Address", IsRequired = true },
                    new UrlFieldDefinition { Id = "website", Name = "website", Label = "Website", Placeholder = "https://example.com" },
                    new TextAreaFieldDefinition { Id = "bio", Name = "bio", Label = "Bio", Rows = 4 },
                    new NumberFieldDefinition { Id = "age", Name = "age", Label = "Age", Min = 18, Max = 120 },
                    new SliderFieldDefinition { Id = "satisfaction", Name = "satisfaction", Label = "Satisfaction (1–10)", Min = 1, Max = 10, Step = 1 },
                    new RadioGroupDefinition
                    {
                        Id = "role",
                        Name = "role",
                        Label = "Role",
                        IsRequired = true,
                        Options =
                        [
                            new FieldOption("developer", "Developer"),
                            new FieldOption("designer", "Designer"),
                            new FieldOption("manager", "Manager"),
                            new FieldOption("other", "Other")
                        ]
                    },
                    new SelectFieldDefinition
                    {
                        Id = "country",
                        Name = "country",
                        Label = "Country",
                        Options =
                        [
                            new FieldOption("gb", "United Kingdom"),
                            new FieldOption("us", "United States"),
                            new FieldOption("au", "Australia"),
                            new FieldOption("other", "Other")
                        ]
                    },
                    new CheckboxGroupDefinition
                    {
                        Id = "interests",
                        Name = "interests",
                        Label = "Interests",
                        Options =
                        [
                            new FieldOption("frontend", "Frontend"),
                            new FieldOption("backend", "Backend"),
                            new FieldOption("devops", "DevOps"),
                            new FieldOption("mobile", "Mobile")
                        ]
                    },
                    new CheckboxFieldDefinition { Id = "agree", Name = "agree", Label = "I agree to the terms", IsRequired = true }
                ]
            }
        }.ToFrozenDictionary(form => form.Id);

        public FormRegistry()
        {
            ValidateForms(forms.Values);
        }

        public FormDefinition? Find(string id) => forms.GetValueOrDefault(id);

        public static void ValidateForms(IEnumerable<FormDefinition> formDefinitions)
        {
            foreach (var form in formDefinitions)
            {
                if (form.EmailOptions.ReplyToFieldName is null)
                {
                    continue;
                }

                var replyToField = form.Fields.FirstOrDefault(field => field.Name == form.EmailOptions.ReplyToFieldName);

                if (replyToField is null)
                {
                    throw new InvalidOperationException(
                        $"Form '{form.Id}': ReplyToFieldName '{form.EmailOptions.ReplyToFieldName}' does not match any field.");
                }

                if (replyToField is not EmailFieldDefinition)
                {
                    throw new InvalidOperationException(
                        $"Form '{form.Id}': ReplyToFieldName '{form.EmailOptions.ReplyToFieldName}' must reference an email field.");
                }
            }
        }
    }
}