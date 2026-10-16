using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using FormHandler.Api.Domain;
using FormHandler.Api.Domain.Fields;
using FormHandler.Api.Domain.Submissions;
using FormHandler.Api.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace FormHandler.Benchmarks
{
    [MemoryDiagnoser]
    public class FieldValueMapperBenchmarks
    {
        private static readonly FormDefinition SmallForm = FormDefinition.Create("small", "Small Form", new FormSubmissionEmailOptions { To = BenchmarkEmail.Email("test@example.com") }, [
                new TextFieldDefinition { Id = "f1", Name = "firstName", Label = "First Name", IsRequired = true },
                new TextFieldDefinition { Id = "f2", Name = "lastName", Label = "Last Name", IsRequired = true },
                new EmailFieldDefinition { Id = "f3", Name = "email", Label = "Email Address", IsRequired = true },
                new TextAreaFieldDefinition { Id = "f4", Name = "comments", Label = "Comments", IsRequired = true },
            ]).Form!;

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

        private static readonly JsonDocument SmallJsonDocument = JsonDocument.Parse(
            """{"firstName":"Alice","lastName":"Smith","email":"alice@example.com","comments":"Hello there"}""");

        private static readonly Dictionary<string, JsonElement> SmallJsonBody =
            SmallJsonDocument.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        private static readonly JsonDocument LargeJsonDocument = JsonDocument.Parse(
            """{"firstName":"Alice","lastName":"Smith","email":"alice@example.com","website":"https://example.com","enquiryType":"sales","interests":["product","pricing"],"preferredContact":"email","teamSize":5,"satisfaction":8,"comments":"Hello there","newsletter":true}""");

        private static readonly Dictionary<string, JsonElement> LargeJsonBody =
            LargeJsonDocument.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        private static readonly FormCollection SmallFormData = new(new Dictionary<string, StringValues>
        {
            ["firstName"] = new StringValues("Alice"),
            ["lastName"] = new StringValues("Smith"),
            ["email"] = new StringValues("alice@example.com"),
            ["comments"] = new StringValues("Hello there"),
        });

        private static readonly FormCollection LargeFormData = new(new Dictionary<string, StringValues>
        {
            ["firstName"] = new StringValues("Alice"),
            ["lastName"] = new StringValues("Smith"),
            ["email"] = new StringValues("alice@example.com"),
            ["website"] = new StringValues("https://example.com"),
            ["enquiryType"] = new StringValues("sales"),
            ["interests"] = new StringValues(["product", "pricing"]),
            ["preferredContact"] = new StringValues("email"),
            ["teamSize"] = new StringValues("5"),
            ["satisfaction"] = new StringValues("8"),
            ["comments"] = new StringValues("Hello there"),
            ["newsletter"] = new StringValues("on"),
        });

        [Benchmark] public IReadOnlyDictionary<string, FieldValue> FromJsonSmall() => FieldValueMapper.FromJson(SmallForm, SmallJsonBody);

        [Benchmark] public IReadOnlyDictionary<string, FieldValue> FromJsonLarge() => FieldValueMapper.FromJson(LargeForm, LargeJsonBody);

        [Benchmark] public IReadOnlyDictionary<string, FieldValue> FromFormEncodedSmall() => FieldValueMapper.FromFormEncoded(SmallForm, SmallFormData);

        [Benchmark] public IReadOnlyDictionary<string, FieldValue> FromFormEncodedLarge() => FieldValueMapper.FromFormEncoded(LargeForm, LargeFormData);
    }
}
