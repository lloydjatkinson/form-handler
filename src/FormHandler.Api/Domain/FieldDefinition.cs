namespace FormHandler.Api.Domain
{
    public abstract class FieldDefinition
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string Label { get; init; }
        public bool IsRequired { get; init; }
        public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();
    }
}
