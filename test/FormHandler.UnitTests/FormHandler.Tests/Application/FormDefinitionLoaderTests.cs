using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormHandler.Api.Domain;
using FormHandler.Api.Domain.Fields;
using FormHandler.Api.Infrastructure.Forms;
using Shouldly;

namespace FormHandler.Tests.Application
{
    public sealed class YamlFormDefinitionLoaderTests : IDisposable
    {
        private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        public YamlFormDefinitionLoaderTests()
        {
            Directory.CreateDirectory(this.tempDirectory);
        }

        public void Dispose()
        {
            Directory.Delete(this.tempDirectory, recursive: true);
        }

        private void WriteFile(string fileName, string content) =>
            File.WriteAllText(Path.Combine(this.tempDirectory, fileName), content);

        private IReadOnlyList<FormDefinition> Load() =>
            new YamlFormDefinitionLoader().Load(this.tempDirectory);

        [Fact]
        public void NonExistentDirectoryThrows()
        {
            var loader = new YamlFormDefinitionLoader();
            Should.Throw<InvalidOperationException>(() => loader.Load("/nonexistent/path"));
        }

        [Fact]
        public void EmptyDirectoryReturnsEmptyList()
        {
            this.Load().ShouldBeEmpty();
        }

        [Fact]
        public void NonYamlFilesAreIgnored()
        {
            this.WriteFile("readme.txt", "hello");
            this.WriteFile("data.json", "{}");
            this.Load().ShouldBeEmpty();
        }

        [Fact]
        public void ValidMultipleFilesReturnMultipleForms()
        {
            this.WriteFile("form1.yaml", """
                id: form1
                name: Form One
                emailOptions:
                  to: one@example.com
                fields: []
                """);
            this.WriteFile("form2.yaml", """
                id: form2
                name: Form Two
                emailOptions:
                  to: two@example.com
                fields: []
                """);

            var forms = this.Load();
            forms.Count.ShouldBe(2);
            forms.Select(form => form.Id).ShouldBe(["form1", "form2"], ignoreOrder: true);
        }

        [Fact]
        public void EmptyYamlFileProducesDeserializationFailure()
        {
            this.WriteFile("empty.yaml", "");

            var exception = Should.Throw<FormLoadException>(() => this.Load());
            var error = exception.Errors.ShouldHaveSingleItem();
            error.Kind.ShouldBe(FormLoadErrorKind.DeserializationFailure);
            error.FilePath.ShouldContain("empty.yaml");
        }

        [Fact]
        public void MalformedYamlProducesDeserializationFailure()
        {
            this.WriteFile("bad.yaml", "id: :\n  invalid: [yaml");

            var exception = Should.Throw<FormLoadException>(() => this.Load());
            exception.Errors.ShouldHaveSingleItem().Kind.ShouldBe(FormLoadErrorKind.DeserializationFailure);
            exception.Errors[0].FilePath.ShouldContain("bad.yaml");
        }

        [Fact]
        public void UnknownFieldTypeProducesDeserializationFailure()
        {
            // The type discriminator resolves the field class from the 'type' key during
            // deserialization, so an unknown type now fails there rather than during mapping.
            this.WriteFile("unknown.yaml", """
                id: test
                name: Test
                emailOptions:
                  to: test@example.com
                fields:
                  - type: foobar
                    id: field1
                    name: field1
                    label: Field One
                """);

            var exception = Should.Throw<FormLoadException>(() => this.Load());
            exception.Errors.ShouldHaveSingleItem().Kind.ShouldBe(FormLoadErrorKind.DeserializationFailure);
        }

        [Fact]
        public void MissingFormIdProducesMappingFailure()
        {
            this.WriteFile("noid.yaml", """
                name: Test
                emailOptions:
                  to: test@example.com
                fields: []
                """);

            var exception = Should.Throw<FormLoadException>(() => this.Load());
            var error = exception.Errors.ShouldHaveSingleItem();
            error.Kind.ShouldBe(FormLoadErrorKind.MappingFailure);
            error.Message.ShouldContain("id");
        }

        [Fact]
        public void MissingEmailOptionsProducesMappingFailure()
        {
            this.WriteFile("noemail.yaml", """
                id: test
                name: Test
                fields: []
                """);

            var exception = Should.Throw<FormLoadException>(() => this.Load());
            var error = exception.Errors.ShouldHaveSingleItem();
            error.Kind.ShouldBe(FormLoadErrorKind.MappingFailure);
            error.Message.ShouldContain("emailOptions");
        }

        [Fact]
        public void InvalidToAddressProducesMappingFailure()
        {
            this.WriteFile("badto.yaml", """
                id: test
                name: Test
                emailOptions:
                  to: "."
                fields: []
                """);

            var exception = Should.Throw<FormLoadException>(() => this.Load());
            var error = exception.Errors.ShouldHaveSingleItem();
            error.Kind.ShouldBe(FormLoadErrorKind.MappingFailure);
            error.Message.ShouldContain("to");
        }

        [Fact]
        public void MissingFieldNameProducesMappingFailure()
        {
            this.WriteFile("noname.yaml", """
                id: test
                name: Test
                emailOptions:
                  to: test@example.com
                fields:
                  - type: text
                    id: first-name
                    label: First Name
                """);

            var exception = Should.Throw<FormLoadException>(() => this.Load());
            var error = exception.Errors.ShouldHaveSingleItem();
            error.Kind.ShouldBe(FormLoadErrorKind.MappingFailure);
            error.Message.ShouldContain("name");
        }

        [Fact]
        public void EmptyOptionsOnSelectProducesMappingFailure()
        {
            this.WriteFile("emptyoptions.yaml", """
                id: test
                name: Test
                emailOptions:
                  to: test@example.com
                fields:
                  - type: select
                    id: country
                    name: country
                    label: Country
                    options: []
                """);

            var exception = Should.Throw<FormLoadException>(() => this.Load());
            var error = exception.Errors.ShouldHaveSingleItem();
            error.Kind.ShouldBe(FormLoadErrorKind.MappingFailure);
            error.Message.ShouldContain("options");
        }

        [Fact]
        public void MinGreaterThanMaxProducesMappingFailure()
        {
            this.WriteFile("badrange.yaml", """
                id: test
                name: Test
                emailOptions:
                  to: test@example.com
                fields:
                  - type: number
                    id: age
                    name: age
                    label: Age
                    min: 100
                    max: 0
                """);

            var exception = Should.Throw<FormLoadException>(() => this.Load());
            var error = exception.Errors.ShouldHaveSingleItem();
            error.Kind.ShouldBe(FormLoadErrorKind.MappingFailure);
            error.Message.ShouldContain("min");
        }

        [Fact]
        public void DuplicateFormIdsProduceDuplicateIdError()
        {
            this.WriteFile("a.yaml", """
                id: inbox
                name: Inbox A
                emailOptions:
                  to: a@example.com
                fields: []
                """);
            this.WriteFile("b.yaml", """
                id: inbox
                name: Inbox B
                emailOptions:
                  to: b@example.com
                fields: []
                """);

            var exception = Should.Throw<FormLoadException>(() => this.Load());
            exception.Errors.Count.ShouldBe(2);
            exception.Errors.ShouldAllBe(error => error.Kind == FormLoadErrorKind.DuplicateId);
        }

        [Fact]
        public void ReplyToFieldNameReferencingNonEmailFieldProducesMappingFailure()
        {
            this.WriteFile("bad-reply-to.yaml", """
                id: test
                name: Test
                emailOptions:
                  to: test@example.com
                  replyToFieldName: name
                fields:
                  - type: text
                    id: name
                    name: name
                    label: Name
                """);

            var exception = Should.Throw<FormLoadException>(() => this.Load());
            var error = exception.Errors.ShouldHaveSingleItem();
            error.Kind.ShouldBe(FormLoadErrorKind.MappingFailure);
        }

        [Fact]
        public void MultipleInvalidFilesProducesAllErrors()
        {
            this.WriteFile("a.yaml", "id: :\n  bad: [yaml");
            this.WriteFile("b.yaml", """
                name: Missing id
                emailOptions:
                  to: test@example.com
                fields: []
                """);

            var exception = Should.Throw<FormLoadException>(() => this.Load());
            exception.Errors.Count.ShouldBe(2);
        }

        [Fact]
        public void AllFieldTypesAreDeserializedCorrectly()
        {
            this.WriteFile("all-fields.yaml", """
                id: all
                name: All Fields
                emailOptions:
                  to: test@example.com
                fields:
                  - type: text
                    id: text1
                    name: text1
                    label: Text
                  - type: textArea
                    id: textarea1
                    name: textarea1
                    label: Textarea
                  - type: email
                    id: email1
                    name: email1
                    label: Email
                  - type: url
                    id: url1
                    name: url1
                    label: Url
                  - type: checkBox
                    id: checkbox1
                    name: checkbox1
                    label: Checkbox
                  - type: number
                    id: number1
                    name: number1
                    label: Number
                  - type: slider
                    id: slider1
                    name: slider1
                    label: Slider
                  - type: select
                    id: select1
                    name: select1
                    label: Select
                    options:
                      - value: a
                        label: A
                  - type: radio
                    id: radio1
                    name: radio1
                    label: Radio
                    options:
                      - value: a
                        label: A
                  - type: checkBoxGroup
                    id: checkboxgroup1
                    name: checkboxgroup1
                    label: Checkbox Group
                    options:
                      - value: a
                        label: A
                """);

            var form = this.Load().ShouldHaveSingleItem();
            form.Fields[0].ShouldBeOfType<TextFieldDefinition>();
            form.Fields[1].ShouldBeOfType<TextAreaFieldDefinition>();
            form.Fields[2].ShouldBeOfType<EmailFieldDefinition>();
            form.Fields[3].ShouldBeOfType<UrlFieldDefinition>();
            form.Fields[4].ShouldBeOfType<CheckboxFieldDefinition>();
            form.Fields[5].ShouldBeOfType<NumberFieldDefinition>();
            form.Fields[6].ShouldBeOfType<SliderFieldDefinition>();
            form.Fields[7].ShouldBeOfType<SelectFieldDefinition>();
            form.Fields[8].ShouldBeOfType<RadioGroupDefinition>();
            form.Fields[9].ShouldBeOfType<CheckboxGroupDefinition>();
        }

        [Fact]
        public void BrandingDefaultsWhenOmitted()
        {
            this.WriteFile("no-branding.yaml", """
                id: test
                name: Test
                emailOptions:
                  to: test@example.com
                fields: []
                """);

            var form = this.Load().ShouldHaveSingleItem();
            form.EmailOptions.Branding.PrimaryColor.ShouldBe("#0066cc");
            form.EmailOptions.Branding.SecondaryColor.ShouldBe("#333333");
        }

        [Fact]
        public void FieldIsDisabledOptionIsRespected()
        {
            this.WriteFile("disabled.yaml", """
                id: test
                name: Test
                emailOptions:
                  to: test@example.com
                fields:
                  - type: select
                    id: country
                    name: country
                    label: Country
                    options:
                      - value: gb
                        label: UK
                      - value: xx
                        label: Disabled
                        isDisabled: true
                """);

            var form = this.Load().ShouldHaveSingleItem();
            var field = form.Fields.ShouldHaveSingleItem().ShouldBeOfType<SelectFieldDefinition>();
            field.Options[0].IsDisabled.ShouldBeFalse();
            field.Options[1].IsDisabled.ShouldBeTrue();
        }
    }
}
