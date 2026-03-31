using FormHandler.Api.Domain.Submissions;

namespace FormHandler.Api.Application.Submissions
{
    public interface IFormSubmissionStore
    {
        Task SaveAsync(FormSubmission submission, FormSubmissionMetadata metadata, CancellationToken cancellationToken = default);

        Task<List<StoredFormSubmission>> GetSubmissionsAsync(string formId, CancellationToken cancellationToken = default);

        Task<StoredFormSubmission?> GetSubmissionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    }
}