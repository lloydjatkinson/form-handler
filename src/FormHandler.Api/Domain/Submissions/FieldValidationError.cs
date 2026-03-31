namespace FormHandler.Api.Domain.Submissions
{
    public sealed class FieldValidationError
    {
        public required string FieldName { get; init; }
        public required string Message { get; init; }
    }
}
