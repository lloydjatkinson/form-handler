using FormHandler.Api.Application.Submissions;
using FormHandler.Api.Domain;

namespace FormHandler.Api.Application.Email
{
    public interface ISubmissionEmailComposer
    {
        EmailComposeResult Compose(FormDefinition form, StoredFormSubmission submission);
    }
}