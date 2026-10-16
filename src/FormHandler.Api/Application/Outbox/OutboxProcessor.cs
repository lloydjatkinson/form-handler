using FormHandler.Api.Application.Email;
using FormHandler.Api.Application.Forms;
using FormHandler.Api.Application.Submissions;
using Microsoft.Extensions.Options;

namespace FormHandler.Api.Application.Outbox
{
    public sealed partial class OutboxProcessor : BackgroundService
    {
        private readonly ILogger<OutboxProcessor> logger;
        private readonly TimeProvider timeProvider;
        private readonly IFormDefinitionStore formDefinitionStore;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ISubmissionEmailComposer emailComposer;
        private readonly IEmailSender emailSender;
        private readonly OutboxOptions options;

        public OutboxProcessor(
            ILogger<OutboxProcessor> logger,
            TimeProvider timeProvider,
            IFormDefinitionStore formDefinitionStore,
            IServiceScopeFactory scopeFactory,
            ISubmissionEmailComposer emailComposer,
            IEmailSender emailSender,
            IOptions<OutboxOptions> options)
        {
            this.logger = logger;
            this.timeProvider = timeProvider;
            this.formDefinitionStore = formDefinitionStore;
            this.scopeFactory = scopeFactory;
            this.emailComposer = emailComposer;
            this.emailSender = emailSender;
            this.options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var periodicTimer = new PeriodicTimer(this.options.PollInterval, this.timeProvider);

            do
            {
                try
                {
                    await this.ProcessBatchAsync(stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    this.LogProcessorError(exception);
                }
            }
            while (await periodicTimer.WaitForNextTickAsync(stoppingToken));
        }

        internal async Task ProcessBatchAsync(CancellationToken cancellationToken)
        {
            await using var scope = this.scopeFactory.CreateAsyncScope();
            var submissionStore = scope.ServiceProvider.GetRequiredService<IFormSubmissionStore>();
            var outboxStore = scope.ServiceProvider.GetRequiredService<IFormSubmissionOutboxStore>();

            var entries = await outboxStore.ClaimPendingAsync(cancellationToken);
            if (entries.Count == 0)
            {
                return;
            }

            foreach (var entry in entries)
            {
                await this.ProcessEntryAsync(entry, submissionStore, outboxStore, cancellationToken);
            }
        }

        private async Task ProcessEntryAsync(
            PendingOutboxEntry entry,
            IFormSubmissionStore submissionStore,
            IFormSubmissionOutboxStore outboxStore,
            CancellationToken cancellationToken)
        {
            try
            {
                var submission = await submissionStore.GetSubmissionByIdAsync(entry.SubmissionId, cancellationToken);
                if (submission is null)
                {
                    this.LogSubmissionNotFound(entry.SubmissionId);
                    await this.RecordFailureAsync(outboxStore, entry.OutboxId, entry.SubmissionId, "Submission not found", cancellationToken);
                    return;
                }

                var form = this.formDefinitionStore.FindById(submission.FormId);
                if (form is null)
                {
                    this.LogFormNotFound(submission.FormId, entry.SubmissionId);
                    await this.RecordFailureAsync(outboxStore, entry.OutboxId, entry.SubmissionId, $"Form '{submission.FormId}' not found", cancellationToken);
                    return;
                }

                switch (this.emailComposer.Compose(form, submission))
                {
                    case EmailComposeResult.Composed { Email: var email }:
                        var sendResult = await this.emailSender.SendAsync(email, form.EmailOptions, cancellationToken);

                        if (sendResult is EmailSendResult.Sent)
                        {
                            await outboxStore.MarkAsSentAsync(entry.OutboxId, cancellationToken);
                            this.LogEntrySent(entry.SubmissionId);
                            break;
                        }

                        if (sendResult is EmailSendResult.Failed { Reason: var sendReason })
                        {
                            await this.RecordFailureAsync(outboxStore, entry.OutboxId, entry.SubmissionId, sendReason, cancellationToken);
                            break;
                        }

                        await this.RecordFailureAsync(outboxStore, entry.OutboxId, entry.SubmissionId, "Email send returned an unexpected result", cancellationToken);
                        break;

                    case EmailComposeResult.Failed { Reason: var reason }:
                        await this.RecordFailureAsync(outboxStore, entry.OutboxId, entry.SubmissionId, reason, cancellationToken);
                        break;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                this.LogEntryProcessingFailed(exception, entry.SubmissionId);
                await this.RecordFailureAsync(outboxStore, entry.OutboxId, entry.SubmissionId, exception.Message, cancellationToken);
            }
        }

        private async Task RecordFailureAsync(
            IFormSubmissionOutboxStore outboxStore,
            Guid outboxId,
            Guid submissionId,
            string reason,
            CancellationToken cancellationToken)
        {
            var result = await outboxStore.MarkAsFailedAsync(outboxId, reason, cancellationToken);

            if (result is OutboxFailureResult.RetriesExhausted)
            {
                this.LogEntryDeadLettered(submissionId, reason);
                return;
            }

            this.LogEntryFailed(submissionId, reason);
        }

        [LoggerMessage(Level = LogLevel.Error, Message = "Submission {SubmissionId} not found in store")]
        private partial void LogSubmissionNotFound(Guid submissionId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Form {FormId} not found for submission {SubmissionId}")]
        private partial void LogFormNotFound(string formId, Guid submissionId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Submission {SubmissionId} sent")]
        private partial void LogEntrySent(Guid submissionId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Submission {SubmissionId} failed: {Reason}")]
        private partial void LogEntryFailed(Guid submissionId, string reason);

        [LoggerMessage(Level = LogLevel.Error, Message = "Submission {SubmissionId} dead-lettered: {Reason}")]
        private partial void LogEntryDeadLettered(Guid submissionId, string reason);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process submission {SubmissionId}")]
        private partial void LogEntryProcessingFailed(Exception exception, Guid submissionId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Outbox processor error")]
        private partial void LogProcessorError(Exception exception);
    }
}