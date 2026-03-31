namespace FormHandler.Api.Domain.Fields
{
    public sealed class TextAreaFieldDefinition : FieldDefinition
    {
        public int? Rows { get; init; }
        public int? Cols { get; init; }
    }
}