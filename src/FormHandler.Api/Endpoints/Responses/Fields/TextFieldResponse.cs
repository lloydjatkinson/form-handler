namespace FormHandler.Api.Endpoints.Responses.Fields
{
    public sealed class TextFieldResponse : FieldResponse
    {
        public int? MaxLength { get; init; }
        public string? Placeholder { get; init; }
    }
}