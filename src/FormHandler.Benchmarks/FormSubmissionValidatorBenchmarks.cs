using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using FormHandler.Api.Domain;
using FormHandler.Api.Domain.Fields;
using FormHandler.Api.Domain.Submissions;

namespace FormHandler.Benchmarks
{
    [MemoryDiagnoser]
    public class FormSubmissionValidatorBenchmarks
    {
        private static readonly FormSubmissionValidator Validator = new();

        private static readonly FormDefinition SmallForm = FormDefinition.Create("small", "Small Form", new FormSubmissionEmailOptions { To = BenchmarkEmail.Email("test@example.com") }, [
                new TextFieldDefinition { Id = "f1", Name = "firstName", Label = "First Name", IsRequired = true },
                new TextFieldDefinition { Id = "f2", Name = "lastName", Label = "Last Name", IsRequired = true },
                new EmailFieldDefinition { Id = "f3", Name = "email", Label = "Email Address", IsRequired = true },
                new TextAreaFieldDefinition { Id = "f4", Name = "comments", Label = "Comments", IsRequired = true },
            ]).Form!;

        private static readonly FormSubmission SmallValid = new()
        {
            FormId = "small",
            Values = new Dictionary<string, FieldValue>
            {
                ["firstName"] = new FieldValue.Text("Alice"),
                ["lastName"] = new FieldValue.Text("Smith"),
                ["email"] = new FieldValue.Text("alice@example.com"),
                ["comments"] = new FieldValue.Text("Hello there"),
            }
        };

        private static readonly FormSubmission SmallInvalid = new()
        {
            FormId = "small",
            Values = new Dictionary<string, FieldValue>
            {
                ["firstName"] = new FieldValue.Text("Alice"),
            }
        };

        private static readonly FormDefinition LargeForm = FormDefinition.Create("large", "Large Form", new FormSubmissionEmailOptions { To = BenchmarkEmail.Email("test@example.com") }, [
                new TextFieldDefinition { Id = "f1", Name = "firstName", Label = "First Name", IsRequired = true },
                new TextFieldDefinition { Id = "f2", Name = "lastName", Label = "Last Name", IsRequired = true },
                new EmailFieldDefinition { Id = "f3", Name = "email", Label = "Email Address", IsRequired = true },
                new UrlFieldDefinition { Id = "f4", Name = "website", Label = "Website", IsRequired = false },
                new RadioGroupDefinition { Id = "f5", Name = "enquiryType", Label = "Enquiry Type", IsRequired = true, Options = [new("sales", "Sales"), new("support", "Support"), new("other", "Other")] },
                new CheckboxGroupDefinition { Id = "f6", Name = "interests", Label = "Interests", IsRequired = false, Options = [new("product", "Product"), new("pricing", "Pricing"), new("demo", "Demo")] },
                new SelectFieldDefinition { Id = "f7", Name = "preferredContact", Label = "Preferred Contact", IsRequired = false, Options = [new("email", "Email"), new("phone", "Phone")] },
                new NumberFieldDefinition { Id = "f8", Name = "teamSize", Label = "Team Size", IsRequired = false, Min = 0, Max = 10000 },
                new SliderFieldDefinition { Id = "f9", Name = "satisfaction", Label = "Satisfaction", IsRequired = false, Min = 1, Max = 10 },
                new TextAreaFieldDefinition { Id = "f10", Name = "comments", Label = "Comments", IsRequired = true },
                new CheckboxFieldDefinition { Id = "f11", Name = "newsletter", Label = "Newsletter", IsRequired = false },
            ]).Form!;

        private static readonly FormSubmission LargeValid = new()
        {
            FormId = "large",
            Values = new Dictionary<string, FieldValue>
            {
                ["firstName"] = new FieldValue.Text("Alice"),
                ["lastName"] = new FieldValue.Text("Smith"),
                ["email"] = new FieldValue.Text("alice@example.com"),
                ["website"] = new FieldValue.Text("https://example.com"),
                ["enquiryType"] = new FieldValue.Selection(["sales"]),
                ["interests"] = new FieldValue.Selection(["product", "pricing"]),
                ["preferredContact"] = new FieldValue.Selection(["email"]),
                ["teamSize"] = new FieldValue.Number(5),
                ["satisfaction"] = new FieldValue.Number(8),
                ["comments"] = new FieldValue.Text("Hello there"),
                ["newsletter"] = new FieldValue.Toggle(true),
            }
        };

        private static readonly FormSubmission LargeInvalid = new()
        {
            FormId = "large",
            Values = new Dictionary<string, FieldValue>
            {
                ["firstName"] = new FieldValue.Text("Alice"),
                ["enquiryType"] = new FieldValue.Selection(["invalid-option"]),
            }
        };

        [Benchmark] public FormSubmissionResult ValidateSmallValid() => Validator.Validate(SmallForm, SmallValid);

        [Benchmark] public FormSubmissionResult ValidateSmallInvalid() => Validator.Validate(SmallForm, SmallInvalid);

        [Benchmark] public FormSubmissionResult ValidateLargeValid() => Validator.Validate(LargeForm, LargeValid);

        [Benchmark] public FormSubmissionResult ValidateLargeInvalid() => Validator.Validate(LargeForm, LargeInvalid);
    }
}
