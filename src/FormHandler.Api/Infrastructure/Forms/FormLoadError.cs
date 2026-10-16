namespace FormHandler.Api.Infrastructure.Forms
{
    public sealed record FormLoadError(string FilePath, FormLoadErrorKind Kind, string Message);

    public enum FormLoadErrorKind
    {
        DeserializationFailure,
        MappingFailure,
        SemanticValidationFailure,
        DuplicateId
    }
}
