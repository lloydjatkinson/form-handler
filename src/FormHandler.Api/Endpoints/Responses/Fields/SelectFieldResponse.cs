namespace FormHandler.Api.Endpoints.Responses.Fields
{
    public sealed class SelectFieldResponse : FieldResponse
    {
        public required IReadOnlyList<FieldOptionResponse> Options { get; init; }
    }
}
