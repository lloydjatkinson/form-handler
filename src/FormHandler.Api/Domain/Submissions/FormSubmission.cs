namespace FormHandler.Api.Domain.Submissions
{
    public sealed class FormSubmission
    {
        public required string FormId { get; init; }
        public required IReadOnlyDictionary<string, FieldValue> Values { get; init; }
    }
}
