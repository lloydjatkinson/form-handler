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

            if (!MailboxAddress.TryParse(emailOptions.To, out var to))
            {
                return new EmailSendResult.Failed($"Invalid recipient address: {emailOptions.To}");
            }

            message.To.Add(to);

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

            try
            {
                using var client = new SmtpClient();
                await client.ConnectAsync(this.options.Smtp.Host, this.options.Smtp.Port, SecureSocketOptions.Auto, cancellationToken);
                await client.AuthenticateAsync(this.options.Smtp.Username, this.options.Smtp.Password, cancellationToken);
                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(quit: true, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return new EmailSendResult.Failed(exception);
            }

            return new EmailSendResult.Sent();
        }
    }
}