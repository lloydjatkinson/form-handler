using System.Collections.Generic;
using Bogus;
using FormHandler.Api.Domain;
using FormHandler.Api.Domain.Fields;
using FormHandler.Api.Domain.Submissions;
using Shouldly;

namespace FormHandler.Tests.Domain.Submissions
{
    public sealed class FormSubmissionValidatorTests
    {
        private readonly FormSubmissionValidator validator = new();
        private readonly Faker faker = new();

        private FormSubmission Submission(string formId, Dictionary<string, FieldValue> values) =>
            new() { FormId = formId, Values = values };

        [Fact]
        public void EmptyFormReturnsSuccess() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, []).Form!,
                this.Submission("contact", []))
            .ShouldBeOfType<FormSubmissionResult.Success>();

        [Fact]
        public void OptionalFieldNotSubmittedReturnsSuccess() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new TextFieldDefinition { Id = "company", Name = "company", Label = "Company" }]).Form!,
                this.Submission("contact", []))
            .ShouldBeOfType<FormSubmissionResult.Success>();

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void OptionalFieldSubmittedBlankReturnsSuccess(string value) =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new TextFieldDefinition { Id = "company", Name = "company", Label = "Company" }]).Form!,
                this.Submission("contact", new() { ["company"] = new FieldValue.Text(value) }))
            .ShouldBeOfType<FormSubmissionResult.Success>();

        [Fact]
        public void RequiredFieldNotSubmittedReturnsInvalid() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new TextFieldDefinition { Id = "full-name", Name = "name", Label = "Name", IsRequired = true }]).Form!,
                this.Submission("contact", []))
            .ShouldBeOfType<FormSubmissionResult.Invalid>();

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void RequiredFieldSubmittedBlankReturnsInvalid(string value) =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new TextFieldDefinition { Id = "full-name", Name = "name", Label = "Name", IsRequired = true }]).Form!,
                this.Submission("contact", new() { ["name"] = new FieldValue.Text(value) }))
            .ShouldBeOfType<FormSubmissionResult.Invalid>();

        [Fact]
        public void RequiredChoiceFieldWithAllWhitespaceValuesReturnsInvalid() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new CheckboxGroupDefinition { Id = "interests", Name = "interests", Label = "Interests", IsRequired = true, Options = [new FieldOption("a", "A")] }]).Form!,
                this.Submission("contact", new() { ["interests"] = new FieldValue.Selection(["", "  "]) }))
            .ShouldBeOfType<FormSubmissionResult.Invalid>();

        [Fact]
        public void UnknownFieldReturnsInvalid() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new TextFieldDefinition { Id = "full-name", Name = "name", Label = "Name", IsRequired = true }]).Form!,
                this.Submission("contact", new() { ["name"] = new FieldValue.Text("Jane"), ["utm_source"] = new FieldValue.Text("google") }))
            .ShouldBeOfType<FormSubmissionResult.Invalid>();

        [Fact]
        public void UnknownFieldErrorIncludesFieldName()
        {
            var result = this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new TextFieldDefinition { Id = "full-name", Name = "name", Label = "Name", IsRequired = true }]).Form!,
                this.Submission("contact", new() { ["name"] = new FieldValue.Text("Jane"), ["utm_source"] = new FieldValue.Text("google") }));

            result.ShouldBeOfType<FormSubmissionResult.Invalid>()
                .Errors.ShouldHaveSingleItem()
                .FieldName.ShouldBe("utm_source");
        }

        [Fact]
        public void MultipleUnknownFieldsEachReturnAnError()
        {
            var result = this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, []).Form!,
                this.Submission("contact", new() { ["utm_source"] = new FieldValue.Text("google"), ["sneaky"] = new FieldValue.Text("data") }));

            result.ShouldBeOfType<FormSubmissionResult.Invalid>()
                .Errors.Count.ShouldBe(2);
        }

        [Fact]
        public void ValidChoiceSelectionReturnsSuccess() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new RadioGroupDefinition { Id = "enquiry-type", Name = "enquiryType", Label = "Enquiry Type", Options = [new FieldOption("sales", "Sales"), new FieldOption("support", "Support")] }]).Form!,
                this.Submission("contact", new() { ["enquiryType"] = new FieldValue.Selection(["support"]) }))
            .ShouldBeOfType<FormSubmissionResult.Success>();

        [Fact]
        public void CheckboxGroupAcceptsMultipleSelections() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new CheckboxGroupDefinition { Id = "favourite-fruits", Name = "fruits", Label = "Fruits", Options = [new FieldOption("apples", "Apples"), new FieldOption("oranges", "Oranges"), new FieldOption("bananas", "Bananas")] }]).Form!,
                this.Submission("contact", new() { ["fruits"] = new FieldValue.Selection(["apples", "bananas"]) }))
            .ShouldBeOfType<FormSubmissionResult.Success>();

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OptionalCheckboxAcceptsAnyToggleValue(bool value) =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new CheckboxFieldDefinition { Id = "newsletter", Name = "newsletter", Label = "Newsletter" }]).Form!,
                this.Submission("contact", new() { ["newsletter"] = new FieldValue.Toggle(value) }))
            .ShouldBeOfType<FormSubmissionResult.Success>();

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RequiredCheckboxValidationReflectsToggleValue(bool value)
        {
            var result = this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new CheckboxFieldDefinition { Id = "newsletter", Name = "newsletter", Label = "Newsletter", IsRequired = true }]).Form!,
                this.Submission("contact", new() { ["newsletter"] = new FieldValue.Toggle(value) }));

            if (value)
                result.ShouldBeOfType<FormSubmissionResult.Success>();
            else
                result.ShouldBeOfType<FormSubmissionResult.Invalid>();
        }

        [Fact]
        public void NumberFieldAcceptsNumericValue() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new NumberFieldDefinition { Id = "age", Name = "age", Label = "Age" }]).Form!,
                this.Submission("contact", new() { ["age"] = new FieldValue.Number(42) }))
            .ShouldBeOfType<FormSubmissionResult.Success>();

        [Fact]
        public void NumberFieldBelowMinReturnsInvalid() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new NumberFieldDefinition { Id = "age", Name = "age", Label = "Age", Min = 18 }]).Form!,
                this.Submission("contact", new() { ["age"] = new FieldValue.Number(5) }))
            .ShouldBeOfType<FormSubmissionResult.Invalid>();

        [Fact]
        public void NumberFieldAboveMaxReturnsInvalid() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new NumberFieldDefinition { Id = "age", Name = "age", Label = "Age", Max = 100 }]).Form!,
                this.Submission("contact", new() { ["age"] = new FieldValue.Number(150) }))
            .ShouldBeOfType<FormSubmissionResult.Invalid>();

        [Fact]
        public void NumberFieldWithinRangeReturnsSuccess() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new NumberFieldDefinition { Id = "age", Name = "age", Label = "Age", Min = 18, Max = 100 }]).Form!,
                this.Submission("contact", new() { ["age"] = new FieldValue.Number(25) }))
            .ShouldBeOfType<FormSubmissionResult.Success>();

        [Fact]
        public void RequiredNumberFieldNotSubmittedReturnsInvalid() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new NumberFieldDefinition { Id = "age", Name = "age", Label = "Age", IsRequired = true }]).Form!,
                this.Submission("contact", []))
            .ShouldBeOfType<FormSubmissionResult.Invalid>();

        [Fact]
        public void SliderFieldAcceptsNumericValue() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new SliderFieldDefinition { Id = "volume", Name = "volume", Label = "Volume", Min = 0, Max = 10 }]).Form!,
                this.Submission("contact", new() { ["volume"] = new FieldValue.Number(7) }))
            .ShouldBeOfType<FormSubmissionResult.Success>();

        [Fact]
        public void NullOnOptionalCheckboxReturnsSuccess() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new CheckboxFieldDefinition { Id = "newsletter", Name = "newsletter", Label = "Newsletter" }]).Form!,
                this.Submission("contact", new() { ["newsletter"] = null! }))
            .ShouldBeOfType<FormSubmissionResult.Success>();

        [Fact]
        public void NullOnRequiredCheckboxReturnsRequiredError()
        {
            var result = this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new CheckboxFieldDefinition { Id = "newsletter", Name = "newsletter", Label = "Newsletter", IsRequired = true }]).Form!,
                this.Submission("contact", new() { ["newsletter"] = null! }))
            .ShouldBeOfType<FormSubmissionResult.Invalid>();

            result.Errors.ShouldHaveSingleItem().Message.ShouldContain("required", Case.Insensitive);
        }

        [Fact]
        public void NonChoiceFieldWithValueReturnsSuccess() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new TextFieldDefinition { Id = "message", Name = "message", Label = "Message" }]).Form!,
                this.Submission("contact", new() { ["message"] = new FieldValue.Text("Hello") }))
            .ShouldBeOfType<FormSubmissionResult.Success>();

        [Fact]
        public void UnrecognisedChoiceOptionReturnsInvalid() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new RadioGroupDefinition { Id = "enquiry-type", Name = "enquiryType", Label = "Enquiry Type", Options = [new FieldOption("sales", "Sales"), new FieldOption("support", "Support")] }]).Form!,
                this.Submission("contact", new() { ["enquiryType"] = new FieldValue.Selection(["partnership"]) }))
            .ShouldBeOfType<FormSubmissionResult.Invalid>();

        [Fact]
        public void RadioGroupRejectsMultipleSelections() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new RadioGroupDefinition { Id = "enquiry-type", Name = "enquiryType", Label = "Enquiry Type", Options = [new FieldOption("sales", "Sales"), new FieldOption("support", "Support")] }]).Form!,
                this.Submission("contact", new() { ["enquiryType"] = new FieldValue.Selection(["sales", "support"]) }))
            .ShouldBeOfType<FormSubmissionResult.Invalid>();

        [Fact]
        public void SingleSelectRejectsMultipleSelections() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new SelectFieldDefinition { Id = "preferred-contact", Name = "preferredContact", Label = "Preferred Contact", Options = [new FieldOption("email", "Email"), new FieldOption("phone", "Phone")] }]).Form!,
                this.Submission("contact", new() { ["preferredContact"] = new FieldValue.Selection(["email", "phone"]) }))
            .ShouldBeOfType<FormSubmissionResult.Invalid>();

        [Fact]
        public void RequiredErrorContainsFieldLabel()
        {
            var label = this.faker.Lorem.Word();
            var form = FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new TextFieldDefinition { Id = "full-name", Name = "name", Label = label, IsRequired = true }]).Form!;

            var result = this.validator.Validate(form, this.Submission("contact", []))
                .ShouldBeOfType<FormSubmissionResult.Invalid>();

            result.Errors.ShouldContain(error => error.Message.Contains(label));
        }

        [Fact]
        public void RequiredErrorHasCorrectFieldName()
        {
            var form = FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new TextFieldDefinition { Id = "email-address", Name = "email", Label = "Email Address", IsRequired = true }]).Form!;

            var result = this.validator.Validate(form, this.Submission("contact", []))
                .ShouldBeOfType<FormSubmissionResult.Invalid>();

            result.Errors.ShouldHaveSingleItem().FieldName.ShouldBe("email");
        }

        [Fact]
        public void OneErrorReturnedPerMissingRequiredField()
        {
            var form = FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") },
            [
                new TextFieldDefinition { Id = "first-name", Name = "firstName", Label = "First Name", IsRequired = true },
                new TextFieldDefinition { Id = "last-name", Name = "lastName", Label = "Last Name", IsRequired = true },
                new EmailFieldDefinition { Id = "email-address", Name = "email", Label = "Email Address", IsRequired = true }
            ]).Form!;

            var result = this.validator.Validate(form, this.Submission("contact", []))
                .ShouldBeOfType<FormSubmissionResult.Invalid>();

            result.Errors.Count.ShouldBe(3);
        }

        [Fact]
        public void RequiredChoiceFieldMissingProducesOnlyRequiredError()
        {
            var form = FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new SelectFieldDefinition { Id = "enquiry-type", Name = "enquiryType", Label = "Enquiry Type", IsRequired = true, Options = [new FieldOption("sales", "Sales"), new FieldOption("support", "Support")] }]).Form!;

            var result = this.validator.Validate(form, this.Submission("contact", []))
                .ShouldBeOfType<FormSubmissionResult.Invalid>();

            result.Errors.ShouldHaveSingleItem().Message.ShouldContain("required", Case.Insensitive);
        }

        [Fact]
        public void InvalidSelectionErrorContainsFieldLabel()
        {
            var label = this.faker.Lorem.Word();
            var form = FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new RadioGroupDefinition { Id = "preferred-contact", Name = "preferredContact", Label = label, Options = [new FieldOption("email", "Email"), new FieldOption("phone", "Phone")] }]).Form!;

            var result = this.validator.Validate(form, this.Submission("contact", new() { ["preferredContact"] = new FieldValue.Selection(["post"]) }))
                .ShouldBeOfType<FormSubmissionResult.Invalid>();

            result.Errors.ShouldContain(error => error.Message.Contains(label));
        }

        [Fact]
        public void InvalidSelectionErrorHasCorrectFieldName()
        {
            var form = FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new RadioGroupDefinition { Id = "preferred-contact", Name = "preferredContact", Label = "Preferred Contact", Options = [new FieldOption("email", "Email"), new FieldOption("phone", "Phone")] }]).Form!;

            var result = this.validator.Validate(form, this.Submission("contact", new() { ["preferredContact"] = new FieldValue.Selection(["post"]) }))
                .ShouldBeOfType<FormSubmissionResult.Invalid>();

            result.Errors.ShouldHaveSingleItem().FieldName.ShouldBe("preferredContact");
        }

        [Fact]
        public void MultipleSelectionsErrorContainsFieldLabel()
        {
            var label = this.faker.Lorem.Word();
            var form = FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new RadioGroupDefinition { Id = "enquiry-type", Name = "enquiryType", Label = label, Options = [new FieldOption("sales", "Sales"), new FieldOption("support", "Support")] }]).Form!;

            var result = this.validator.Validate(form, this.Submission("contact", new() { ["enquiryType"] = new FieldValue.Selection(["sales", "support"]) }))
                .ShouldBeOfType<FormSubmissionResult.Invalid>();

            result.Errors.ShouldContain(error => error.Message.Contains(label));
        }

        [Fact]
        public void CheckboxGroupRejectsInvalidOptionValue() =>
            this.validator.Validate(
                FormDefinition.Create("contact", "Contact", new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") }, [new CheckboxGroupDefinition { Id = "interests", Name = "interests", Label = "Interests", Options = [new FieldOption("frontend", "Frontend"), new FieldOption("backend", "Backend")] }]).Form!,
                this.Submission("contact", new() { ["interests"] = new FieldValue.Selection(["mobile"]) }))
            .ShouldBeOfType<FormSubmissionResult.Invalid>();
    }
}