using FormHandler.Api.Application.Email;
using FormHandler.Api.Domain;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FormHandler.Api.Infrastructure.Email
{
    public sealed class MailKitEmailSender : IEmailSender
    {
        private readonly EmailOptions options;

        public MailKitEmailSender(IOptions<EmailOptions> options)
        {
            this.options = options.Value;
        }

        public async Task<EmailSendResult> SendAsync(ComposedEmail email, FormSubmissionEmailOptions emailOptions, CancellationToken cancellationToken = default)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(this.options.From.Name, this.options.From.Address));

            message.To.Add(new MailboxAddress(emailOptions.To.DisplayName, emailOptions.To.Address));

            foreach (var address in emailOptions.Cc)
            {
                if (!MailboxAddress.TryParse(address, out var cc))
                {
                    return new EmailSendResult.Failed($"Invalid CC address: {address}");
                }

                message.Cc.Add(cc);
            }

            foreach (var address in emailOptions.Bcc)
            {
                if (!MailboxAddress.TryParse(address, out var bcc))
                {
                    return new EmailSendResult.Failed($"Invalid BCC address: {address}");
                }

                message.Bcc.Add(bcc);
            }

            message.Subject = email.Subject;

            if (email.ReplyTo is not null && MailboxAddress.TryParse(email.ReplyTo, out var replyTo))
            {
                message.ReplyTo.Add(replyTo);
            }

            var body = new BodyBuilder
            {
                HtmlBody = email.HtmlBody,
                TextBody = email.PlainTextBody,
            };

            message.Body = body.ToMessageBody();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(this.options.Smtp.Timeout);
            var sendToken = timeoutCts.Token;

            try
            {
                using var client = new SmtpClient
                {
                    Timeout = Convert.ToInt32(this.options.Smtp.Timeout.TotalMilliseconds),
                };
                await client.ConnectAsync(this.options.Smtp.Host, this.options.Smtp.Port, SecureSocketOptions.Auto, sendToken);
                await client.AuthenticateAsync(this.options.Smtp.Username, this.options.Smtp.Password, sendToken);
                await client.SendAsync(message, sendToken);
                await client.DisconnectAsync(quit: true, sendToken);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return new EmailSendResult.Failed($"SMTP operation timed out after {this.options.Smtp.Timeout.TotalSeconds:0}s");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return new EmailSendResult.Failed(exception);
            }

            return new EmailSendResult.Sent();
        }
    }
}