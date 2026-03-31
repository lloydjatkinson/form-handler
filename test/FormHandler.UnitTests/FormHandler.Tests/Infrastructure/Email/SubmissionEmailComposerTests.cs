using System;
using System.Collections.Generic;
using System.Linq;
using FormHandler.Api.Application.Email;
using FormHandler.Api.Application.Submissions;
using FormHandler.Api.Domain;
using FormHandler.Api.Domain.Fields;
using FormHandler.Api.Domain.Submissions;
using FormHandler.Api.Infrastructure.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace FormHandler.Tests.Infrastructure.Email
{
    public sealed class SubmissionEmailComposerTests
    {
        private SubmissionEmailComposer CreateComposer() =>
            new(NullLogger<SubmissionEmailComposer>.Instance, new EmbeddedResourceEmailTemplateProvider());

        private FormDefinition CreateForm(string? replyToFieldName = null) => new()
        {
            Id = "test",
            Name = "Test Form",
            EmailOptions = new FormSubmissionEmailOptions { To = "test@example.com", ReplyToFieldName = replyToFieldName },
            Fields =
            [
                new TextFieldDefinition { Id = "name", Name = "name", Label = "Full Name" },
                new EmailFieldDefinition { Id = "email", Name = "email", Label = "Email Address" }
            ]
        };

        private StoredFormSubmission CreateSubmission(FormDefinition form) => new()
        {
            Id = Guid.NewGuid(),
            FormId = form.Id,
            SubmittedAt = new DateTimeOffset(2026, 3, 25, 12, 0, 0, TimeSpan.Zero),
            Metadata = new FormSubmissionMetadata(null, null, null, null, null, null, null, null),
            Values = form.Fields.ToDictionary(field => field.Name, _ => (FieldValue)new FieldValue.Text("test"))
        };

        private string RenderToPlainText(FieldDefinition field, FieldValue value)
        {
            var form = new FormDefinition
            {
                Id = "test",
                Name = "Test Form",
                EmailOptions = new FormSubmissionEmailOptions { To = "test@example.com" },
                Fields = [field]
            };
            var submission = new StoredFormSubmission
            {
                Id = Guid.NewGuid(),
                FormId = "test",
                SubmittedAt = new DateTimeOffset(2026, 3, 25, 12, 0, 0, TimeSpan.Zero),
                Metadata = new FormSubmissionMetadata(null, null, null, null, null, null, null, null),
                Values = new Dictionary<string, FieldValue> { [field.Name] = value }
            };
            return this.CreateComposer().Compose(form, submission).ShouldBeOfType<EmailComposeResult.Composed>().Email.PlainTextBody;
        }

        [Fact]
        public void ComposeReturnsComposedResult()
        {
            var form = this.CreateForm();
            this.CreateComposer().Compose(form, this.CreateSubmission(form)).ShouldBeOfType<EmailComposeResult.Composed>();
        }

        [Fact]
        public void InvalidMjmlSyntaxReturnsFailure()
        {
            var form = this.CreateForm();
            var templateProvider = Substitute.For<IEmailTemplateProvider>();
            templateProvider.GetTemplate(Arg.Any<string>()).Returns("<mjml><invalid></invalid></mjml>");
            var composer = new SubmissionEmailComposer(NullLogger<SubmissionEmailComposer>.Instance, templateProvider);

            composer.Compose(form, this.CreateSubmission(form)).ShouldBeOfType<EmailComposeResult.Failed>();
        }

        [Fact]
        public void InvalidScribanSyntaxReturnsFailure()
        {
            var form = this.CreateForm();
            var templateProvider = Substitute.For<IEmailTemplateProvider>();
            templateProvider.GetTemplate(Arg.Any<string>()).Returns("{{ if }}");
            var composer = new SubmissionEmailComposer(NullLogger<SubmissionEmailComposer>.Instance, templateProvider);

            composer.Compose(form, this.CreateSubmission(form)).ShouldBeOfType<EmailComposeResult.Failed>();
        }

        [Fact]
        public void SubjectContainsFormName()
        {
            var form = this.CreateForm();
            var result = this.CreateComposer().Compose(form, this.CreateSubmission(form)).ShouldBeOfType<EmailComposeResult.Composed>();

            result.Email.Subject.ShouldBe("New submission for form \"Test Form\"");
        }

        [Fact]
        public void HtmlBodyIsNotEmpty()
        {
            var form = this.CreateForm();
            var result = this.CreateComposer().Compose(form, this.CreateSubmission(form)).ShouldBeOfType<EmailComposeResult.Composed>();

            result.Email.HtmlBody.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public void PlainTextBodyContainsFieldLabelsAndValues()
        {
            var form = new FormDefinition
            {
                Id = "test",
                Name = "Test Form",
                EmailOptions = new FormSubmissionEmailOptions { To = "test@example.com" },
                Fields =
                [
                    new TextFieldDefinition { Id = "name", Name = "name", Label = "Full Name" },
                    new EmailFieldDefinition { Id = "email", Name = "email", Label = "Email Address" }
                ]
            };
            var submission = new StoredFormSubmission
            {
                Id = Guid.NewGuid(),
                FormId = "test",
                SubmittedAt = new DateTimeOffset(2026, 3, 25, 12, 0, 0, TimeSpan.Zero),
                Metadata = new FormSubmissionMetadata(null, null, null, null, null, null, null, null),
                Values = new Dictionary<string, FieldValue>
                {
                    ["name"] = new FieldValue.Text("Alice Smith"),
                    ["email"] = new FieldValue.Text("alice@example.com")
                }
            };

            var result = this.CreateComposer().Compose(form, submission).ShouldBeOfType<EmailComposeResult.Composed>();

            result.Email.PlainTextBody.ShouldContain("Full Name");
            result.Email.PlainTextBody.ShouldContain("Alice Smith");
            result.Email.PlainTextBody.ShouldContain("Email Address");
            result.Email.PlainTextBody.ShouldContain("alice@example.com");
        }

        [Fact]
        public void ReplyToIsNullWhenReplyToFieldNameIsNotConfigured()
        {
            var form = this.CreateForm();
            var result = this.CreateComposer().Compose(form, this.CreateSubmission(form)).ShouldBeOfType<EmailComposeResult.Composed>();

            result.Email.ReplyTo.ShouldBeNull();
        }

        [Fact]
        public void ReplyToIsEmailFieldValueWhenReplyToFieldNameIsConfigured()
        {
            var form = new FormDefinition
            {
                Id = "test",
                Name = "Test Form",
                EmailOptions = new FormSubmissionEmailOptions { To = "test@example.com", ReplyToFieldName = "email" },
                Fields = [new EmailFieldDefinition { Id = "email", Name = "email", Label = "Email Address" }]
            };
            var submission = new StoredFormSubmission
            {
                Id = Guid.NewGuid(),
                FormId = "test",
                SubmittedAt = new DateTimeOffset(2026, 3, 25, 12, 0, 0, TimeSpan.Zero),
                Metadata = new FormSubmissionMetadata(null, null, null, null, null, null, null, null),
                Values = new Dictionary<string, FieldValue> { ["email"] = new FieldValue.Text("alice@example.com") }
            };

            var result = this.CreateComposer().Compose(form, submission).ShouldBeOfType<EmailComposeResult.Composed>();

            result.Email.ReplyTo.ShouldBe("alice@example.com");
        }

        [Fact]
        public void ReplyToIsNullWhenReplyToFieldNameDoesNotMatchAnyField()
        {
            var form = this.CreateForm(replyToFieldName: "nonexistent");
            var result = this.CreateComposer().Compose(form, this.CreateSubmission(form)).ShouldBeOfType<EmailComposeResult.Composed>();

            result.Email.ReplyTo.ShouldBeNull();
        }

        [Fact]
        public void ReplyToIsNullWhenFieldNotPresentInSubmission()
        {
            var form = new FormDefinition
            {
                Id = "test",
                Name = "Test Form",
                EmailOptions = new FormSubmissionEmailOptions { To = "test@example.com", ReplyToFieldName = "email" },
                Fields = [new EmailFieldDefinition { Id = "email", Name = "email", Label = "Email Address" }]
            };
            var submission = new StoredFormSubmission
            {
                Id = Guid.NewGuid(),
                FormId = "test",
                SubmittedAt = new DateTimeOffset(2026, 3, 25, 12, 0, 0, TimeSpan.Zero),
                Metadata = new FormSubmissionMetadata(null, null, null, null, null, null, null, null),
                Values = new Dictionary<string, FieldValue>()
            };

            var result = this.CreateComposer().Compose(form, submission).ShouldBeOfType<EmailComposeResult.Composed>();

            result.Email.ReplyTo.ShouldBeNull();
        }

        [Fact]
        public void NumberFieldRendersAsNumericString()
        {
            var plainText = this.RenderToPlainText(
                new NumberFieldDefinition { Id = "score", Name = "score", Label = "Score" },
                new FieldValue.Number(42));

            plainText.ShouldContain("42");
        }

        [Fact]
        public void ToggledOnCheckboxRendersAsYes()
        {
            var plainText = this.RenderToPlainText(
                new CheckboxFieldDefinition { Id = "newsletter", Name = "newsletter", Label = "Newsletter" },
                new FieldValue.Toggle(true));

            plainText.ShouldContain("Yes");
        }

        [Fact]
        public void ToggledOffCheckboxRendersAsNo()
        {
            var plainText = this.RenderToPlainText(
                new CheckboxFieldDefinition { Id = "newsletter", Name = "newsletter", Label = "Newsletter" },
                new FieldValue.Toggle(false));

            plainText.ShouldContain("No");
        }

        [Fact]
        public void SelectionFieldRendersOptionLabels()
        {
            var plainText = this.RenderToPlainText(
                new RadioGroupDefinition { Id = "role", Name = "role", Label = "Role", Options = [new FieldOption("dev", "Developer"), new FieldOption("des", "Designer")] },
                new FieldValue.Selection(["dev", "des"]));

            plainText.ShouldContain("Developer");
            plainText.ShouldContain("Designer");
        }

        [Fact]
        public void FieldAbsentFromSubmissionRendersAsEmptyValue()
        {
            var form = new FormDefinition
            {
                Id = "test",
                Name = "Test Form",
                EmailOptions = new FormSubmissionEmailOptions { To = "test@example.com" },
                Fields = [new TextFieldDefinition { Id = "notes", Name = "notes", Label = "Notes" }]
            };
            var submission = new StoredFormSubmission
            {
                Id = Guid.NewGuid(),
                FormId = "test",
                SubmittedAt = new DateTimeOffset(2026, 3, 25, 12, 0, 0, TimeSpan.Zero),
                Metadata = new FormSubmissionMetadata(null, null, null, null, null, null, null, null),
                Values = new Dictionary<string, FieldValue>()
            };

            var result = this.CreateComposer().Compose(form, submission).ShouldBeOfType<EmailComposeResult.Composed>();

            result.Email.PlainTextBody.ShouldContain("Notes:");
        }

        [Fact]
        public void WarningIsLoggedWhenReplyToFieldNameDoesNotMatchAnyField()
        {
            var logger = Substitute.For<ILogger<SubmissionEmailComposer>>();
            var composer = new SubmissionEmailComposer(logger, new EmbeddedResourceEmailTemplateProvider());

            var form = this.CreateForm(replyToFieldName: "nonexistent");
            composer.Compose(form, this.CreateSubmission(form));

            logger.Received().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
    }
}
