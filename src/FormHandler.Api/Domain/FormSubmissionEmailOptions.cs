namespace FormHandler.Api.Domain
{
    public sealed class FormSubmissionEmailOptions
    {
        public required EmailAddress To { get; init; }
        public IReadOnlyList<string> Cc { get; init; } = [];
        public IReadOnlyList<string> Bcc { get; init; } = [];
        public FormSubmissionEmailBranding Branding { get; init; } = new();
        public string? ReplyToFieldName { get; init; }
    }
}
