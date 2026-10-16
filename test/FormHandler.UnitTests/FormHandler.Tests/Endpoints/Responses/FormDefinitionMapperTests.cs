using System.Collections.Generic;
using FormHandler.Api.Domain;
using FormHandler.Api.Domain.Fields;
using FormHandler.Api.Endpoints.Responses;
using FormHandler.Api.Endpoints.Responses.Fields;
using Shouldly;

namespace FormHandler.Tests.Endpoints.Responses
{
    public sealed class FormDefinitionMapperTests
    {
        [Fact]
        public void FormPropertiesAreMapped()
        {
            var form = FormDefinition.Create("test-form", "Test Form", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, []).Form!;

            var response = FormDefinitionMapper.ToResponse(form);

            response.Id.ShouldBe("test-form");
            response.Name.ShouldBe("Test Form");
        }

        [Fact]
        public void AllFieldTypesMapToCorrectResponseType()
        {
            var form = FormDefinition.Create("test-form", "Test Form", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [
                    new TextFieldDefinition { Id = "full-name", Name = "fullName", Label = "Full Name" },
                    new EmailFieldDefinition { Id = "email-address", Name = "emailAddress", Label = "Email Address" },
                    new UrlFieldDefinition { Id = "website-url", Name = "websiteUrl", Label = "Website URL" },
                    new TextAreaFieldDefinition { Id = "biography", Name = "biography", Label = "Biography" },
                    new CheckboxFieldDefinition { Id = "newsletter", Name = "newsletter", Label = "Newsletter" },
                    new NumberFieldDefinition { Id = "age", Name = "age", Label = "Age" },
                    new SliderFieldDefinition { Id = "satisfaction", Name = "satisfaction", Label = "Satisfaction" },
                    new SelectFieldDefinition { Id = "country", Name = "country", Label = "Country", Options = [new FieldOption("gb", "United Kingdom")] },
                    new RadioGroupDefinition { Id = "role", Name = "role", Label = "Role", Options = [new FieldOption("developer", "Developer")] },
                    new CheckboxGroupDefinition { Id = "interests", Name = "interests", Label = "Interests", Options = [new FieldOption("frontend", "Frontend")] }
                ]).Form!;

            var fields = FormDefinitionMapper.ToResponse(form).Fields;

            fields[0].ShouldBeOfType<TextFieldResponse>();
            fields[1].ShouldBeOfType<EmailFieldResponse>();
            fields[2].ShouldBeOfType<UrlFieldResponse>();
            fields[3].ShouldBeOfType<TextAreaFieldResponse>();
            fields[4].ShouldBeOfType<CheckboxFieldResponse>();
            fields[5].ShouldBeOfType<NumberFieldResponse>();
            fields[6].ShouldBeOfType<SliderFieldResponse>();
            fields[7].ShouldBeOfType<SelectFieldResponse>();
            fields[8].ShouldBeOfType<RadioGroupResponse>();
            fields[9].ShouldBeOfType<CheckboxGroupResponse>();
        }

        [Fact]
        public void BaseFieldPropertiesAreMapped()
        {
            var form = FormDefinition.Create("test-form", "Test Form", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new TextFieldDefinition { Id = "first-name", Name = "firstName", Label = "First Name", IsRequired = true }]).Form!;

            var field = FormDefinitionMapper.ToResponse(form).Fields.ShouldHaveSingleItem();

            field.Id.ShouldBe("first-name");
            field.Name.ShouldBe("firstName");
            field.Label.ShouldBe("First Name");
            field.IsRequired.ShouldBeTrue();
        }

        [Fact]
        public void TextFieldSpecificPropertiesAreMapped()
        {
            var form = FormDefinition.Create("test-form", "Test Form", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new TextFieldDefinition { Id = "full-name", Name = "fullName", Label = "Full Name", MaxLength = 200, Placeholder = "Enter text" }]).Form!;

            var field = FormDefinitionMapper.ToResponse(form).Fields.ShouldHaveSingleItem().ShouldBeOfType<TextFieldResponse>();

            field.MaxLength.ShouldBe(200);
            field.Placeholder.ShouldBe("Enter text");
        }

        [Fact]
        public void SelectFieldOptionsMapped()
        {
            var form = FormDefinition.Create("test-form", "Test Form", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new SelectFieldDefinition { Id = "country", Name = "country", Label = "Country", Options = [new FieldOption("gb", "United Kingdom")] }]).Form!;

            var field = FormDefinitionMapper.ToResponse(form).Fields.ShouldHaveSingleItem().ShouldBeOfType<SelectFieldResponse>();

            field.Options.ShouldHaveSingleItem().Value.ShouldBe("gb");
        }

        [Fact]
        public void ChoiceFieldOptionsAreMapped()
        {
            var form = FormDefinition.Create("test-form", "Test Form", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new RadioGroupDefinition
                {
                    Id = "agree-terms",
                    Name = "agreeTerms",
                    Label = "Agree to Terms",
                    Options = [new FieldOption("yes", "Yes"), new FieldOption("no", "No", isDisabled: true)]
                }]).Form!;

            var options = FormDefinitionMapper.ToResponse(form).Fields.ShouldHaveSingleItem().ShouldBeOfType<RadioGroupResponse>().Options;

            options.Count.ShouldBe(2);
            options[0].Value.ShouldBe("yes");
            options[0].Label.ShouldBe("Yes");
            options[0].IsDisabled.ShouldBeFalse();
            options[1].Value.ShouldBe("no");
            options[1].IsDisabled.ShouldBeTrue();
        }

        [Fact]
        public void NumberFieldSpecificPropertiesAreMapped()
        {
            var form = FormDefinition.Create("test-form", "Test Form", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new NumberFieldDefinition { Id = "age", Name = "age", Label = "Age", Min = 18, Max = 120, Step = 1 }]).Form!;

            var field = FormDefinitionMapper.ToResponse(form).Fields.ShouldHaveSingleItem().ShouldBeOfType<NumberFieldResponse>();

            field.Min.ShouldBe(18);
            field.Max.ShouldBe(120);
            field.Step.ShouldBe(1);
        }

        [Fact]
        public void SliderFieldSpecificPropertiesAreMapped()
        {
            var form = FormDefinition.Create("test-form", "Test Form", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new SliderFieldDefinition { Id = "rating", Name = "rating", Label = "Rating", Min = 1, Max = 10, Step = 1 }]).Form!;

            var field = FormDefinitionMapper.ToResponse(form).Fields.ShouldHaveSingleItem().ShouldBeOfType<SliderFieldResponse>();

            field.Min.ShouldBe(1);
            field.Max.ShouldBe(10);
            field.Step.ShouldBe(1);
        }

        [Fact]
        public void TextAreaFieldSpecificPropertiesAreMapped()
        {
            var form = FormDefinition.Create("test-form", "Test Form", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new TextAreaFieldDefinition { Id = "bio", Name = "bio", Label = "Bio", Rows = 4, Cols = 60 }]).Form!;

            var field = FormDefinitionMapper.ToResponse(form).Fields.ShouldHaveSingleItem().ShouldBeOfType<TextAreaFieldResponse>();

            field.Rows.ShouldBe(4);
            field.Cols.ShouldBe(60);
        }

        [Fact]
        public void EmailFieldPlaceholderIsMapped()
        {
            var form = FormDefinition.Create("test-form", "Test Form", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new EmailFieldDefinition { Id = "email", Name = "email", Label = "Email", Placeholder = "you@example.com" }]).Form!;

            var field = FormDefinitionMapper.ToResponse(form).Fields.ShouldHaveSingleItem().ShouldBeOfType<EmailFieldResponse>();

            field.Placeholder.ShouldBe("you@example.com");
        }

        [Fact]
        public void UrlFieldPlaceholderIsMapped()
        {
            var form = FormDefinition.Create("test-form", "Test Form", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new UrlFieldDefinition { Id = "website", Name = "website", Label = "Website", Placeholder = "https://example.com" }]).Form!;

            var field = FormDefinitionMapper.ToResponse(form).Fields.ShouldHaveSingleItem().ShouldBeOfType<UrlFieldResponse>();

            field.Placeholder.ShouldBe("https://example.com");
        }

        [Fact]
        public void FieldAttributesAreMapped()
        {
            var form = FormDefinition.Create("test-form", "Test Form", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new TextFieldDefinition { Id = "email", Name = "email", Label = "Email", Attributes = new Dictionary<string, string> { ["data-autocomplete"] = "email" } }]).Form!;

            var field = FormDefinitionMapper.ToResponse(form).Fields.ShouldHaveSingleItem();

            field.Attributes.ShouldNotBeNull();
            field.Attributes["data-autocomplete"].ShouldBe("email");
        }

        [Fact]
        public void EmptyFieldAttributesMapToNull()
        {
            var form = FormDefinition.Create("test-form", "Test Form", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new TextFieldDefinition { Id = "name", Name = "name", Label = "Name" }]).Form!;

            var field = FormDefinitionMapper.ToResponse(form).Fields.ShouldHaveSingleItem();

            field.Attributes.ShouldBeNull();
        }
    }
}