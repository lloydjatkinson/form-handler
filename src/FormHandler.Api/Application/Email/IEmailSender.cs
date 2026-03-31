using FormHandler.Api.Domain;

namespace FormHandler.Api.Application.Email
{
    public interface IEmailSender
    {
        Task<EmailSendResult> SendAsync(ComposedEmail email, FormSubmissionEmailOptions emailOptions, CancellationToken cancellationToken = default);
    }
}