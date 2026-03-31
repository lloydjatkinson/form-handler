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

        private static readonly JsonDocument SmallJsonDocument = JsonDocument.Parse(
            """{"firstName":"Alice","lastName":"Smith","email":"alice@example.com","message":"Hello there"}""");

        private static readonly Dictionary<string, JsonElement> SmallJsonBody =
            SmallJsonDocument.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        private static readonly JsonDocument LargeJsonDocument = JsonDocument.Parse(
            """{"firstName":"Alice","lastName":"Smith","email":"alice@example.com","company":"ACME","website":"https://example.com","type":["buyer"],"interests":["news","events"],"rating":8,"message":"Hello there","newsletter":true}""");

        private static readonly Dictionary<string, JsonElement> LargeJsonBody =
            LargeJsonDocument.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);

        private static readonly FormCollection SmallFormData = new(new Dictionary<string, StringValues>
        {
            ["firstName"] = new StringValues("Alice"),
            ["lastName"] = new StringValues("Smith"),
            ["email"] = new StringValues("alice@example.com"),
            ["message"] = new StringValues("Hello there"),
        });

        private static readonly FormCollection LargeFormData = new(new Dictionary<string, StringValues>
        {
            ["firstName"] = new StringValues("Alice"),
            ["lastName"] = new StringValues("Smith"),
            ["email"] = new StringValues("alice@example.com"),
            ["company"] = new StringValues("ACME"),
            ["website"] = new StringValues("https://example.com"),
            ["type"] = new StringValues("buyer"),
            ["interests"] = new StringValues(["news", "events"]),
            ["rating"] = new StringValues("8"),
            ["message"] = new StringValues("Hello there"),
            ["newsletter"] = new StringValues("on"),
        });

        [Benchmark] public IReadOnlyDictionary<string, FieldValue> FromJsonSmall() => FieldValueMapper.FromJson(SmallForm, SmallJsonBody);

        [Benchmark] public IReadOnlyDictionary<string, FieldValue> FromJsonLarge() => FieldValueMapper.FromJson(LargeForm, LargeJsonBody);

        [Benchmark] public IReadOnlyDictionary<string, FieldValue> FromFormEncodedSmall() => FieldValueMapper.FromFormEncoded(SmallForm, SmallFormData);

        [Benchmark] public IReadOnlyDictionary<string, FieldValue> FromFormEncodedLarge() => FieldValueMapper.FromFormEncoded(LargeForm, LargeFormData);
    }
}