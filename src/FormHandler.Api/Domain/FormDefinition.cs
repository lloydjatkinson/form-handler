namespace FormHandler.Api.Domain
{
    public sealed class FormDefinition
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required FormSubmissionEmailOptions EmailOptions { get; init; }
        public required IReadOnlyList<FieldDefinition> Fields { get; init; }
        public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();
    }
}
