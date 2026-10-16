namespace FormHandler.Api.Domain.Fields
{
    public sealed class TextFieldDefinition : FieldDefinition
    {
        public int? MaxLength { get; init; }
        public string? Placeholder { get; init; }
    }
}
