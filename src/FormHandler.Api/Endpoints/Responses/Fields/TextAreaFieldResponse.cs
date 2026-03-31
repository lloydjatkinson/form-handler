namespace FormHandler.Api.Endpoints.Responses.Fields
{
    public sealed class TextAreaFieldResponse : FieldResponse
    {
        public int? Rows { get; init; }
        public int? Cols { get; init; }
    }
}