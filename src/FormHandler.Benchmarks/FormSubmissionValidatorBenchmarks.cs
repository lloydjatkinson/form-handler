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

        // Small form: 4 required text fields
        private static readonly FormDefinition SmallForm = new()
        {
            Id = "small",
            Name = "Small Form",
            EmailOptions = new FormSubmissionEmailOptions { To = "test@example.com" },
            Fields =
            [
                new TextFieldDefinition { Id = "f1", Name = "firstName", Label = "First Name", IsRequired = true },
                new TextFieldDefinition { Id = "f2", Name = "lastName", Label = "Last Name", IsRequired = true },
                new EmailFieldDefinition { Id = "f3", Name = "email", Label = "EmailOptions", IsRequired = true },
                new TextAreaFieldDefinition { Id = "f4", Name = "message", Label = "Message", IsRequired = true },
            ]
        };

        private static readonly FormSubmission SmallValid = new()
        {
            FormId = "small",
            Values = new Dictionary<string, FieldValue>
            {
                ["firstName"] = new FieldValue.Text("Alice"),
                ["lastName"] = new FieldValue.Text("Smith"),
                ["email"] = new FieldValue.Text("alice@example.com"),
                ["message"] = new FieldValue.Text("Hello there"),
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

        // Larger form: 10 fields, mixed types
        private static readonly FormDefinition LargeForm = new()
        {
            Id = "large",
            Name = "Large Form",
            EmailOptions = new FormSubmissionEmailOptions { To = "test@example.com" },
            Fields =
            [
                new TextFieldDefinition { Id = "f1", Name = "firstName", Label = "First Name", IsRequired = true },
                new TextFieldDefinition { Id = "f2", Name = "lastName", Label = "Last Name", IsRequired = true },
                new EmailFieldDefinition { Id = "f3", Name = "email", Label = "EmailOptions", IsRequired = true },
                new TextFieldDefinition { Id = "f4", Name = "company", Label = "Company", IsRequired = false },
                new UrlFieldDefinition { Id = "f5", Name = "website", Label = "Website", IsRequired = false },
                new SelectFieldDefinition { Id = "f6", Name = "type", Label = "Type", IsRequired = true, Options = [new("buyer", "Buyer"), new("seller", "Seller")] },
                new CheckboxGroupDefinition { Id = "f7", Name = "interests", Label = "Interests", IsRequired = false, Options = [new("news", "News"), new("offers", "Offers"), new("events", "Events")] },
                new SliderFieldDefinition { Id = "f8", Name = "rating", Label = "Rating", IsRequired = false, Min = 1, Max = 10 },
                new TextAreaFieldDefinition { Id = "f9", Name = "message", Label = "Message", IsRequired = true },
                new CheckboxFieldDefinition { Id = "f10", Name = "newsletter", Label = "Newsletter", IsRequired = false },
            ]
        };

        private static readonly FormSubmission LargeValid = new()
        {
            FormId = "large",
            Values = new Dictionary<string, FieldValue>
            {
                ["firstName"] = new FieldValue.Text("Alice"),
                ["lastName"] = new FieldValue.Text("Smith"),
                ["email"] = new FieldValue.Text("alice@example.com"),
                ["company"] = new FieldValue.Text("ACME"),
                ["website"] = new FieldValue.Text("https://example.com"),
                ["type"] = new FieldValue.Selection(["buyer"]),
                ["interests"] = new FieldValue.Selection(["news", "events"]),
                ["rating"] = new FieldValue.Number(8),
                ["message"] = new FieldValue.Text("Hello there"),
                ["newsletter"] = new FieldValue.Toggle(true),
            }
        };

        private static readonly FormSubmission LargeInvalid = new()
        {
            FormId = "large",
            Values = new Dictionary<string, FieldValue>
            {
                ["firstName"] = new FieldValue.Text("Alice"),
                ["type"] = new FieldValue.Selection(["middleman"]),
            }
        };

        [Benchmark] public FormSubmissionResult ValidateSmallValid() => Validator.Validate(SmallForm, SmallValid);

        [Benchmark] public FormSubmissionResult ValidateSmallInvalid() => Validator.Validate(SmallForm, SmallInvalid);

        [Benchmark] public FormSubmissionResult ValidateLargeValid() => Validator.Validate(LargeForm, LargeValid);

        [Benchmark] public FormSubmissionResult ValidateLargeInvalid() => Validator.Validate(LargeForm, LargeInvalid);
    }
}