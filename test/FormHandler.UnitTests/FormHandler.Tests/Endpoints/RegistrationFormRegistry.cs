using FormHandler.Api.Application.Forms;
using FormHandler.Api.Domain;
using FormHandler.Api.Domain.Fields;
using FormHandler.Api.Infrastructure.Forms;

namespace FormHandler.Tests.Endpoints
{
    internal static class RegistrationFormDefinitionStore
    {
        internal static IFormDefinitionStore Create() => new InMemoryFormDefinitionStore(
        [
            FormDefinition.Create("registration", "Registration", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") },
            [
                new TextFieldDefinition { Id = "name", Name = "name", Label = "Full Name", IsRequired = true, MaxLength = 200 },
                new TextFieldDefinition { Id = "company", Name = "company", Label = "Company" },
                new EmailFieldDefinition { Id = "email", Name = "email", Label = "Email Address", IsRequired = true },
                new UrlFieldDefinition { Id = "website", Name = "website", Label = "Website" },
                new RadioGroupDefinition
                {
                    Id = "enquiry-type",
                    Name = "enquiryType",
                    Label = "Enquiry Type",
                    IsRequired = true,
                    Options = [new FieldOption("sales", "Sales"), new FieldOption("support", "Support"), new FieldOption("other", "Other")]
                },
                new CheckboxGroupDefinition
                {
                    Id = "interests",
                    Name = "interests",
                    Label = "Interests",
                    IsRequired = true,
                    Options = [new FieldOption("product", "Product"), new FieldOption("pricing", "Pricing"), new FieldOption("demo", "Demo"), new FieldOption("partnership", "Partnership")]
                },
                new SelectFieldDefinition
                {
                    Id = "preferred-contact",
                    Name = "preferredContact",
                    Label = "Preferred Contact Method",
                    Options = [new FieldOption("email", "Email"), new FieldOption("phone", "Phone")]
                },
                new CheckboxGroupDefinition
                {
                    Id = "availability",
                    Name = "availability",
                    Label = "Availability",
                    Options =
                    [
                        new FieldOption("monday", "Monday"),
                        new FieldOption("tuesday", "Tuesday"),
                        new FieldOption("wednesday", "Wednesday"),
                        new FieldOption("thursday", "Thursday"),
                        new FieldOption("friday", "Friday")
                    ]
                },
                new NumberFieldDefinition { Id = "team-size", Name = "teamSize", Label = "Team Size", Min = 0, Max = 10000 },
                new SliderFieldDefinition { Id = "satisfaction", Name = "satisfaction", Label = "Satisfaction", Min = 1, Max = 10, Step = 1 },
                new TextAreaFieldDefinition { Id = "comments", Name = "comments", Label = "Additional Comments" },
                new CheckboxFieldDefinition { Id = "newsletter", Name = "newsletter", Label = "Subscribe to newsletter" }
            ]).Form!
        ]);
    }
}
