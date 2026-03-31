using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FormHandler.Api.Domain;
using FormHandler.Api.Domain.Fields;
using FormHandler.Api.Domain.Submissions;
using FormHandler.Api.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Shouldly;

namespace FormHandler.Tests.Endpoints
{
    public sealed class FieldValueMapperTests
    {
        private FormDefinition FormWith(params FieldDefinition[] fields) =>
            new() { Id = "contact", Name = "Contact", EmailOptions = new FormSubmissionEmailOptions { To = "test@example.com" }, Fields = [.. fields] };

        private CheckboxFieldDefinition Checkbox(string name) =>
            new() { Id = name, Name = name, Label = name };

        private NumberFieldDefinition NumberField(string name) =>
            new() { Id = name, Name = name, Label = name };

        private RadioGroupDefinition RadioGroup(string name) =>
            new() { Id = name, Name = name, Label = name, Options = [new FieldOption("a", "A"), new FieldOption("b", "B")] };

        private CheckboxGroupDefinition CheckboxGroup(string name) =>
            new() { Id = name, Name = name, Label = name, Options = [new FieldOption("x", "X"), new FieldOption("y", "Y")] };

        private Dictionary<string, JsonElement> ParseBody(string json) =>
            JsonDocument.Parse(json).RootElement.EnumerateObject().ToDictionary(pair => pair.Name, pair => pair.Value);

        [Fact]
        public void FromFormEncodedNormalisesUncheckedCheckboxToToggleFalse()
        {
            var form = this.FormWith(this.Checkbox("newsletter"));
            var formData = new FormCollection([]);

            var result = FieldValueMapper.FromFormEncoded(form, formData);

            result["newsletter"].ShouldBe(new FieldValue.Toggle(false));
        }

        [Fact]
        public void FromFormEncodedPreservesCheckedCheckboxAsToggleTrue()
        {
            var form = this.FormWith(this.Checkbox("newsletter"));
            var formData = new FormCollection(new Dictionary<string, StringValues>
            {
                ["newsletter"] = new StringValues("on"),
            });

            var result = FieldValueMapper.FromFormEncoded(form, formData);

            result["newsletter"].ShouldBe(new FieldValue.Toggle(true));
        }

        [Fact]
        public void FromFormEncodedNormalisesMultipleUncheckedCheckboxes()
        {
            var form = this.FormWith(this.Checkbox("terms"), this.Checkbox("newsletter"));
            var formData = new FormCollection([]);

            var result = FieldValueMapper.FromFormEncoded(form, formData);

            result["terms"].ShouldBe(new FieldValue.Toggle(false));
            result["newsletter"].ShouldBe(new FieldValue.Toggle(false));
        }

        [Fact]
        public void FromJsonNormalisesOmittedCheckboxToToggleFalse()
        {
            var form = this.FormWith(this.Checkbox("newsletter"));
            var body = new Dictionary<string, JsonElement>();

            var result = FieldValueMapper.FromJson(form, body);

            result["newsletter"].ShouldBe(new FieldValue.Toggle(false));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FromJsonPreservesExplicitCheckboxValue(bool value)
        {
            var form = this.FormWith(this.Checkbox("newsletter"));
            var body = JsonDocument.Parse($$$"""{"newsletter":{{{value.ToString().ToLower()}}}}""")
                .RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value);

            var result = FieldValueMapper.FromJson(form, body);

            result["newsletter"].ShouldBe(new FieldValue.Toggle(value));
        }

        // FromFormEncoded — field type mapping

        [Fact]
        public void FromFormEncodedMapsTextFieldToText()
        {
            var form = this.FormWith(new TextFieldDefinition { Id = "name", Name = "name", Label = "Name" });
            var formData = new FormCollection(new Dictionary<string, StringValues> { ["name"] = new StringValues("Alice") });

            var result = FieldValueMapper.FromFormEncoded(form, formData);

            result["name"].ShouldBe(new FieldValue.Text("Alice"));
        }

        [Fact]
        public void FromFormEncodedMapsNumericFieldToNumber()
        {
            var form = this.FormWith(this.NumberField("age"));
            var formData = new FormCollection(new Dictionary<string, StringValues> { ["age"] = new StringValues("25") });

            var result = FieldValueMapper.FromFormEncoded(form, formData);

            result["age"].ShouldBe(new FieldValue.Number(25));
        }

        [Fact]
        public void FromFormEncodedThrowsForNonNumericValueOnNumericField()
        {
            var form = this.FormWith(this.NumberField("age"));
            var formData = new FormCollection(new Dictionary<string, StringValues> { ["age"] = new StringValues("not-a-number") });

            Should.Throw<JsonException>(() => FieldValueMapper.FromFormEncoded(form, formData));
        }

        [Fact]
        public void FromFormEncodedMapsSingleChoiceValueToSelection()
        {
            var form = this.FormWith(this.RadioGroup("role"));
            var formData = new FormCollection(new Dictionary<string, StringValues> { ["role"] = new StringValues("a") });

            var result = FieldValueMapper.FromFormEncoded(form, formData);

            result["role"].ShouldBeOfType<FieldValue.Selection>().Values.ShouldBe(["a"]);
        }

        [Fact]
        public void FromFormEncodedMapsMultipleChoiceValuesToSelection()
        {
            var form = this.FormWith(this.CheckboxGroup("interests"));
            var formData = new FormCollection(new Dictionary<string, StringValues> { ["interests"] = new StringValues(["x", "y"]) });

            var result = FieldValueMapper.FromFormEncoded(form, formData);

            result["interests"].ShouldBeOfType<FieldValue.Selection>().Values.ShouldBe(["x", "y"]);
        }

        // FromJson — field type mapping

        [Fact]
        public void FromJsonMapsTextFieldToText()
        {
            var form = this.FormWith(new TextFieldDefinition { Id = "name", Name = "name", Label = "Name" });
            var body = this.ParseBody("""{"name":"Alice"}""");

            var result = FieldValueMapper.FromJson(form, body);

            result["name"].ShouldBe(new FieldValue.Text("Alice"));
        }

        [Fact]
        public void FromJsonMapsNumericFieldToNumber()
        {
            var form = this.FormWith(this.NumberField("age"));
            var body = this.ParseBody("""{"age":25}""");

            var result = FieldValueMapper.FromJson(form, body);

            result["age"].ShouldBe(new FieldValue.Number(25));
        }

        [Fact]
        public void FromJsonMapsChoiceFieldArrayToSelection()
        {
            var form = this.FormWith(this.CheckboxGroup("interests"));
            var body = this.ParseBody("""{"interests":["x","y"]}""");

            var result = FieldValueMapper.FromJson(form, body);

            result["interests"].ShouldBeOfType<FieldValue.Selection>().Values.ShouldBe(["x", "y"]);
        }

        [Fact]
        public void FromJsonMapsChoiceFieldStringToSingleElementSelection()
        {
            var form = this.FormWith(this.RadioGroup("role"));
            var body = this.ParseBody("""{"role":"a"}""");

            var result = FieldValueMapper.FromJson(form, body);

            result["role"].ShouldBeOfType<FieldValue.Selection>().Values.ShouldBe(["a"]);
        }

        [Fact]
        public void FromJsonMapsUnknownStringFieldToText()
        {
            var form = this.FormWith();
            var body = this.ParseBody("""{"utm_source":"google"}""");

            var result = FieldValueMapper.FromJson(form, body);

            result["utm_source"].ShouldBe(new FieldValue.Text("google"));
        }

        [Fact]
        public void FromJsonMapsUnknownNumericFieldToNumber()
        {
            var form = this.FormWith();
            var body = this.ParseBody("""{"score":42}""");

            var result = FieldValueMapper.FromJson(form, body);

            result["score"].ShouldBe(new FieldValue.Number(42));
        }

        [Fact]
        public void FromJsonMapsUnknownBooleanFieldToToggle()
        {
            var form = this.FormWith();
            var body = this.ParseBody("""{"consent":true}""");

            var result = FieldValueMapper.FromJson(form, body);

            result["consent"].ShouldBe(new FieldValue.Toggle(true));
        }

        [Fact]
        public void FromJsonMapsUnknownArrayFieldToSelection()
        {
            var form = this.FormWith();
            var body = this.ParseBody("""{"tags":["a","b"]}""");

            var result = FieldValueMapper.FromJson(form, body);

            result["tags"].ShouldBeOfType<FieldValue.Selection>().Values.ShouldBe(["a", "b"]);
        }

        [Fact]
        public void FromJsonThrowsForUnknownFieldWithObjectValue()
        {
            var form = this.FormWith();
            var body = this.ParseBody("""{"meta":{}}""");

            Should.Throw<JsonException>(() => FieldValueMapper.FromJson(form, body));
        }

        // FromJson — type mismatch errors

        [Fact]
        public void FromJsonThrowsForNonBooleanCheckboxValue()
        {
            var form = this.FormWith(this.Checkbox("newsletter"));
            var body = this.ParseBody("""{"newsletter":"yes"}""");

            Should.Throw<JsonException>(() => FieldValueMapper.FromJson(form, body));
        }

        [Fact]
        public void FromJsonThrowsForNonNumericValueOnNumericField()
        {
            var form = this.FormWith(this.NumberField("age"));
            var body = this.ParseBody("""{"age":"twenty"}""");

            Should.Throw<JsonException>(() => FieldValueMapper.FromJson(form, body));
        }

        [Fact]
        public void FromJsonThrowsForInvalidTypeOnChoiceField()
        {
            var form = this.FormWith(this.RadioGroup("role"));
            var body = this.ParseBody("""{"role":42}""");

            Should.Throw<JsonException>(() => FieldValueMapper.FromJson(form, body));
        }

        [Fact]
        public void FromJsonThrowsForNonStringValueOnTextField()
        {
            var form = this.FormWith(new TextFieldDefinition { Id = "name", Name = "name", Label = "Name" });
            var body = this.ParseBody("""{"name":42}""");

            Should.Throw<JsonException>(() => FieldValueMapper.FromJson(form, body));
        }
    }
}