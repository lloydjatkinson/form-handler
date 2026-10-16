using FormHandler.Api.Domain;
using FormHandler.Api.Domain.Fields;
using Shouldly;

namespace FormHandler.Tests.Application
{
    public sealed class FormDefinitionTests
    {
        [Fact]
        public void ReplyToFieldNameThatMatchesNoFieldProducesError()
        {
            var (form, errors) = FormDefinition.Create(
                "test", "Test",
                new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com"), ReplyToFieldName = "nonexistent" },
                [new TextFieldDefinition { Id = "name", Name = "name", Label = "Name" }]);

            form.ShouldBeNull();
            errors.ShouldContain(error => error.Contains("nonexistent"));
        }

        [Fact]
        public void ReplyToFieldNameReferencingNonEmailFieldProducesError()
        {
            var (form, errors) = FormDefinition.Create(
                "test", "Test",
                new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com"), ReplyToFieldName = "name" },
                [new TextFieldDefinition { Id = "name", Name = "name", Label = "Name" }]);

            form.ShouldBeNull();
            errors.ShouldContain(error => error.Contains("email field"));
        }

        [Fact]
        public void NullReplyToFieldNameProducesNoError()
        {
            var (form, errors) = FormDefinition.Create(
                "test", "Test",
                new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") },
                [new TextFieldDefinition { Id = "name", Name = "name", Label = "Name" }]);

            errors.ShouldBeEmpty();
            form.ShouldNotBeNull();
        }

        [Fact]
        public void ReplyToFieldNameMatchingEmailFieldProducesNoError()
        {
            var (form, errors) = FormDefinition.Create(
                "test", "Test",
                new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com"), ReplyToFieldName = "email" },
                [new EmailFieldDefinition { Id = "email", Name = "email", Label = "Email Address" }]);

            errors.ShouldBeEmpty();
            form.ShouldNotBeNull();
        }

        [Fact]
        public void BlankReplyToFieldNameProducesError()
        {
            var (form, errors) = FormDefinition.Create(
                "test", "Test",
                new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com"), ReplyToFieldName = "   " },
                [new TextFieldDefinition { Id = "name", Name = "name", Label = "Name" }]);

            form.ShouldBeNull();
            errors.ShouldContain(error => error.Contains("blank"));
        }

        [Fact]
        public void DuplicateFieldNamesProducesError()
        {
            var (form, errors) = FormDefinition.Create(
                "test", "Test",
                new FormSubmissionEmailOptions { To = TestEmail.Email("test@example.com") },
                [
                    new TextFieldDefinition { Id = "a", Name = "dup", Label = "A" },
                    new TextFieldDefinition { Id = "b", Name = "dup", Label = "B" }
                ]);

            form.ShouldBeNull();
            errors.ShouldContain(error => error.Contains("duplicate"));
        }
    }
}
