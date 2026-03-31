using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FormHandler.Api.Application;
using FormHandler.Api.Application.Email;
using FormHandler.Api.Application.Outbox;
using FormHandler.Api.Application.Submissions;
using FormHandler.Api.Domain;
using FormHandler.Api.Domain.Submissions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace FormHandler.Tests.Application
{
    public sealed class OutboxProcessorTests
    {
        private readonly FakeLogger<OutboxProcessor> logger = new();
        private readonly FakeTimeProvider timeProvider = new();
        private readonly IFormRegistry formRegistry = Substitute.For<IFormRegistry>();
        private readonly IServiceScopeFactory scopeFactory = Substitute.For<IServiceScopeFactory>();
        private readonly ISubmissionEmailComposer emailComposer = Substitute.For<ISubmissionEmailComposer>();
        private readonly IEmailSender emailSender = Substitute.For<IEmailSender>();
        private readonly IFormSubmissionStore submissionStore = Substitute.For<IFormSubmissionStore>();
        private readonly IFormSubmissionOutboxStore outboxStore = Substitute.For<IFormSubmissionOutboxStore>();

        public OutboxProcessorTests()
        {
            var serviceProvider = Substitute.For<IServiceProvider>();
            var scope = Substitute.For<IServiceScope>();
            scope.ServiceProvider.Returns(serviceProvider);
            this.scopeFactory.CreateScope().Returns(scope);
            serviceProvider.GetService(typeof(IFormSubmissionStore)).Returns(this.submissionStore);
            serviceProvider.GetService(typeof(IFormSubmissionOutboxStore)).Returns(this.outboxStore);
        }

        private OutboxProcessor CreateProcessor() => new(
            this.logger,
            this.timeProvider,
            this.formRegistry,
            this.scopeFactory,
            this.emailComposer,
            this.emailSender,
            Options.Create(new OutboxOptions()));

        [Fact]
        public async Task EmptyOutboxDoesNotSendAnyEmails()
        {
            this.outboxStore
                .ClaimPendingAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyCollection<PendingOutboxEntry>>([]));

            await this.CreateProcessor().ProcessBatchAsync(CancellationToken.None);

            await this.emailSender.DidNotReceive().SendAsync(
                Arg.Any<ComposedEmail>(),
                Arg.Any<FormSubmissionEmailOptions>(),
                Arg.Any<CancellationToken>());
        }

        [Theory, FormHandlerAutoData]
        public async Task SubmissionNotFoundRecordsFailure(PendingOutboxEntry entry)
        {
            this.outboxStore
                .ClaimPendingAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyCollection<PendingOutboxEntry>>([entry]));
            this.submissionStore
                .GetSubmissionByIdAsync(entry.SubmissionId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<StoredFormSubmission?>(null));

            await this.CreateProcessor().ProcessBatchAsync(CancellationToken.None);

            await this.outboxStore.Received(1).MarkAsFailedAsync(entry.OutboxId, Arg.Any<string>(), Arg.Any<CancellationToken>());
            this.logger.Collector.GetSnapshot().ShouldContain(record =>
                record.Level == LogLevel.Error &&
                record.Message.Contains(entry.SubmissionId.ToString()));
            this.logger.Collector.GetSnapshot().ShouldContain(record =>
                record.Level == LogLevel.Warning &&
                record.Message.Contains(entry.SubmissionId.ToString()));
        }

        [Theory, FormHandlerAutoData]
        public async Task FormNotFoundRecordsFailure(PendingOutboxEntry entry, StoredFormSubmission submission)
        {
            this.outboxStore
                .ClaimPendingAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyCollection<PendingOutboxEntry>>([entry]));
            this.submissionStore
                .GetSubmissionByIdAsync(entry.SubmissionId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<StoredFormSubmission?>(submission));
            this.formRegistry.Find(submission.FormId).Returns((FormDefinition?)null);

            await this.CreateProcessor().ProcessBatchAsync(CancellationToken.None);

            await this.outboxStore.Received(1).MarkAsFailedAsync(entry.OutboxId, Arg.Any<string>(), Arg.Any<CancellationToken>());
            this.logger.Collector.GetSnapshot().ShouldContain(record =>
                record.Level == LogLevel.Error &&
                record.Message.Contains(submission.FormId) &&
                record.Message.Contains(entry.SubmissionId.ToString()));
        }

        [Theory, FormHandlerAutoData]
        public async Task CompositionFailureRecordsFailure(PendingOutboxEntry entry, StoredFormSubmission submission, FormDefinition form)
        {
            this.outboxStore
                .ClaimPendingAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyCollection<PendingOutboxEntry>>([entry]));
            this.submissionStore
                .GetSubmissionByIdAsync(entry.SubmissionId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<StoredFormSubmission?>(submission));
            this.formRegistry.Find(submission.FormId).Returns(form);
            this.emailComposer.Compose(form, submission).Returns(new EmailComposeResult.Failed("compose error"));

            await this.CreateProcessor().ProcessBatchAsync(CancellationToken.None);

            await this.outboxStore.Received(1).MarkAsFailedAsync(entry.OutboxId, Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Theory, FormHandlerAutoData]
        public async Task SuccessfulSendMarksEntryAsSent(PendingOutboxEntry entry, StoredFormSubmission submission, FormDefinition form, ComposedEmail email)
        {
            this.outboxStore
                .ClaimPendingAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyCollection<PendingOutboxEntry>>([entry]));
            this.submissionStore
                .GetSubmissionByIdAsync(entry.SubmissionId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<StoredFormSubmission?>(submission));
            this.formRegistry.Find(submission.FormId).Returns(form);
            this.emailComposer.Compose(form, submission).Returns(new EmailComposeResult.Composed(email));
            this.emailSender
                .SendAsync(email, form.EmailOptions, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<EmailSendResult>(new EmailSendResult.Sent()));

            await this.CreateProcessor().ProcessBatchAsync(CancellationToken.None);

            await this.outboxStore.Received(1).MarkAsSentAsync(entry.OutboxId, Arg.Any<CancellationToken>());
        }

        [Theory, FormHandlerAutoData]
        public async Task SendFailureRecordsFailure(PendingOutboxEntry entry, StoredFormSubmission submission, FormDefinition form, ComposedEmail email)
        {
            this.outboxStore
                .ClaimPendingAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyCollection<PendingOutboxEntry>>([entry]));
            this.submissionStore
                .GetSubmissionByIdAsync(entry.SubmissionId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<StoredFormSubmission?>(submission));
            this.formRegistry.Find(submission.FormId).Returns(form);
            this.emailComposer.Compose(form, submission).Returns(new EmailComposeResult.Composed(email));
            this.emailSender
                .SendAsync(email, form.EmailOptions, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<EmailSendResult>(new EmailSendResult.Failed(new SocketException((int)SocketError.ConnectionRefused))));

            await this.CreateProcessor().ProcessBatchAsync(CancellationToken.None);

            await this.outboxStore.Received(1).MarkAsFailedAsync(entry.OutboxId, Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Theory, FormHandlerAutoData]
        public async Task ExceptionDuringProcessingRecordsFailure(PendingOutboxEntry entry)
        {
            this.outboxStore
                .ClaimPendingAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyCollection<PendingOutboxEntry>>([entry]));
            this.submissionStore
                .GetSubmissionByIdAsync(entry.SubmissionId, Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Unexpected error"));

            await this.CreateProcessor().ProcessBatchAsync(CancellationToken.None);

            await this.outboxStore.Received(1).MarkAsFailedAsync(entry.OutboxId, Arg.Any<string>(), Arg.Any<CancellationToken>());
            this.logger.Collector.GetSnapshot().ShouldContain(record =>
                record.Level == LogLevel.Error &&
                record.Message.Contains(entry.SubmissionId.ToString()));
        }

        [Theory, FormHandlerAutoData]
        public async Task AllEntriesInBatchAreSent(
            PendingOutboxEntry entry1, PendingOutboxEntry entry2,
            StoredFormSubmission submission1, StoredFormSubmission submission2,
            FormDefinition form1, FormDefinition form2,
            ComposedEmail email)
        {
            this.outboxStore
                .ClaimPendingAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyCollection<PendingOutboxEntry>>([entry1, entry2]));
            this.submissionStore
                .GetSubmissionByIdAsync(entry1.SubmissionId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<StoredFormSubmission?>(submission1));
            this.submissionStore
                .GetSubmissionByIdAsync(entry2.SubmissionId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<StoredFormSubmission?>(submission2));
            this.formRegistry.Find(submission1.FormId).Returns(form1);
            this.formRegistry.Find(submission2.FormId).Returns(form2);
            this.emailComposer.Compose(form1, submission1).Returns(new EmailComposeResult.Composed(email));
            this.emailComposer.Compose(form2, submission2).Returns(new EmailComposeResult.Composed(email));
            this.emailSender
                .SendAsync(email, Arg.Any<FormSubmissionEmailOptions>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<EmailSendResult>(new EmailSendResult.Sent()));

            await this.CreateProcessor().ProcessBatchAsync(CancellationToken.None);

            await this.outboxStore.Received(1).MarkAsSentAsync(entry1.OutboxId, Arg.Any<CancellationToken>());
            await this.outboxStore.Received(1).MarkAsSentAsync(entry2.OutboxId, Arg.Any<CancellationToken>());
        }

        [Theory, FormHandlerAutoData]
        public async Task BatchContinuesProcessingAfterEntryFailure(
            PendingOutboxEntry entry1, PendingOutboxEntry entry2,
            StoredFormSubmission submission1, StoredFormSubmission submission2,
            FormDefinition form1, FormDefinition form2,
            ComposedEmail email)
        {
            this.outboxStore
                .ClaimPendingAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyCollection<PendingOutboxEntry>>([entry1, entry2]));
            this.submissionStore
                .GetSubmissionByIdAsync(entry1.SubmissionId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<StoredFormSubmission?>(submission1));
            this.submissionStore
                .GetSubmissionByIdAsync(entry2.SubmissionId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<StoredFormSubmission?>(submission2));
            this.formRegistry.Find(submission1.FormId).Returns(form1);
            this.formRegistry.Find(submission2.FormId).Returns(form2);
            this.emailComposer.Compose(form1, submission1).Returns(new EmailComposeResult.Failed("template error"));
            this.emailComposer.Compose(form2, submission2).Returns(new EmailComposeResult.Composed(email));
            this.emailSender
                .SendAsync(email, form2.EmailOptions, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<EmailSendResult>(new EmailSendResult.Sent()));

            await this.CreateProcessor().ProcessBatchAsync(CancellationToken.None);

            await this.outboxStore.Received(1).MarkAsFailedAsync(entry1.OutboxId, Arg.Any<string>(), Arg.Any<CancellationToken>());
            await this.outboxStore.Received(1).MarkAsSentAsync(entry2.OutboxId, Arg.Any<CancellationToken>());
        }

        [Theory, FormHandlerAutoData]
        public async Task BatchContinuesProcessingAfterEntryException(
            PendingOutboxEntry entry1, PendingOutboxEntry entry2,
            StoredFormSubmission submission2,
            FormDefinition form2,
            ComposedEmail email)
        {
            this.outboxStore
                .ClaimPendingAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyCollection<PendingOutboxEntry>>([entry1, entry2]));
            this.submissionStore
                .GetSubmissionByIdAsync(entry1.SubmissionId, Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("store error"));
            this.submissionStore
                .GetSubmissionByIdAsync(entry2.SubmissionId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<StoredFormSubmission?>(submission2));
            this.formRegistry.Find(submission2.FormId).Returns(form2);
            this.emailComposer.Compose(form2, submission2).Returns(new EmailComposeResult.Composed(email));
            this.emailSender
                .SendAsync(email, form2.EmailOptions, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<EmailSendResult>(new EmailSendResult.Sent()));

            await this.CreateProcessor().ProcessBatchAsync(CancellationToken.None);

            await this.outboxStore.Received(1).MarkAsFailedAsync(entry1.OutboxId, Arg.Any<string>(), Arg.Any<CancellationToken>());
            await this.outboxStore.Received(1).MarkAsSentAsync(entry2.OutboxId, Arg.Any<CancellationToken>());
        }

        [Theory, FormHandlerAutoData]
        public async Task DeadLetteredEntryIsLoggedAtErrorLevel(PendingOutboxEntry entry, StoredFormSubmission submission, FormDefinition form, ComposedEmail email)
        {
            this.outboxStore
                .ClaimPendingAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyCollection<PendingOutboxEntry>>([entry]));
            this.submissionStore
                .GetSubmissionByIdAsync(entry.SubmissionId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<StoredFormSubmission?>(submission));
            this.formRegistry.Find(submission.FormId).Returns(form);
            this.emailComposer.Compose(form, submission).Returns(new EmailComposeResult.Composed(email));
            this.emailSender
                .SendAsync(email, form.EmailOptions, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<EmailSendResult>(new EmailSendResult.Failed("smtp error")));
            this.outboxStore
                .MarkAsFailedAsync(entry.OutboxId, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<OutboxFailureResult>(new OutboxFailureResult.RetriesExhausted()));

            await this.CreateProcessor().ProcessBatchAsync(CancellationToken.None);

            this.logger.Collector.GetSnapshot().ShouldContain(record =>
                record.Level == LogLevel.Error &&
                record.Message.Contains("dead-lettered"));
        }
    }
}
