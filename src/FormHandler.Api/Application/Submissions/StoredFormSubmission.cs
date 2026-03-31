using FormHandler.Api.Domain.Submissions;

namespace FormHandler.Api.Application.Submissions
{
    public sealed class StoredFormSubmission
    {
        public required Guid Id { get; init; }
        public required string FormId { get; init; }
        public required IReadOnlyDictionary<string, FieldValue> Values { get; init; }
        public required DateTimeOffset SubmittedAt { get; init; }
        public required FormSubmissionMetadata Metadata { get; init; }
    }
}
