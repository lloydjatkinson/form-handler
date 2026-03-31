namespace FormHandler.Api.Endpoints.Responses.Fields
{
    public sealed class RadioGroupResponse : FieldResponse
    {
        public required IReadOnlyList<FieldOptionResponse> Options { get; init; }
    }
}
