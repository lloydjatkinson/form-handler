namespace FormHandler.Api.Endpoints.Responses.Fields
{
    public sealed class FieldOptionResponse
    {
        public required string Value { get; init; }
        public required string Label { get; init; }
        public bool IsDisabled { get; init; }
    }
}
