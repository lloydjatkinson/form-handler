using System;
using FormHandler.Api.Application;
using FormHandler.Api.Domain;
using FormHandler.Api.Domain.Fields;
using Shouldly;

namespace FormHandler.Tests.Application
{
    public sealed class FormRegistryTests
    {
        [Fact]
        public void ConstructionSucceeds() =>
            Should.NotThrow(() => new FormRegistry());

        [Fact]
        public void FindReturnsFormForKnownId()
        {
            var registry = new FormRegistry();

            registry.Find("inbox").ShouldNotBeNull();
        }

        [Fact]
        public void FindReturnsNullForUnknownId()
        {
            var registry = new FormRegistry();

            registry.Find("unknown").ShouldBeNull();
        }

        [Fact]
        public void ValidateFormsThrowsWhenReplyToFieldNameDoesNotMatchAnyField()
        {
            var form = new FormDefinition
            {
                Id = "test",
                Name = "Test",
                EmailOptions = new FormSubmissionEmailOptions { To = "test@example.com", ReplyToFieldName = "nonexistent" },
                Fields = [new TextFieldDefinition { Id = "name", Name = "name", Label = "Name" }]
            };

            Should.Throw<InvalidOperationException>(() => FormRegistry.ValidateForms([form]))
                .Message.ShouldContain("nonexistent");
        }

        [Fact]
        public void ValidateFormsThrowsWhenReplyToFieldNameReferencesNonEmailField()
        {
            var form = new FormDefinition
            {
                Id = "test",
                Name = "Test",
                EmailOptions = new FormSubmissionEmailOptions { To = "test@example.com", ReplyToFieldName = "name" },
                Fields = [new TextFieldDefinition { Id = "name", Name = "name", Label = "Name" }]
            };

            Should.Throw<InvalidOperationException>(() => FormRegistry.ValidateForms([form]))
                .Message.ShouldContain("email field");
        }

        [Fact]
        public void ValidateFormsDoesNotThrowWhenReplyToFieldNameIsNull()
        {
            var form = new FormDefinition
            {
                Id = "test",
                Name = "Test",
                EmailOptions = new FormSubmissionEmailOptions { To = "test@example.com" },
                Fields = [new TextFieldDefinition { Id = "name", Name = "name", Label = "Name" }]
            };

            Should.NotThrow(() => FormRegistry.ValidateForms([form]));
        }

        [Fact]
        public void ValidateFormsDoesNotThrowWhenReplyToFieldNameMatchesEmailField()
        {
            var form = new FormDefinition
            {
                Id = "test",
                Name = "Test",
                EmailOptions = new FormSubmissionEmailOptions { To = "test@example.com", ReplyToFieldName = "email" },
                Fields = [new EmailFieldDefinition { Id = "email", Name = "email", Label = "Email Address" }]
            };

            Should.NotThrow(() => FormRegistry.ValidateForms([form]));
        }
    }
}