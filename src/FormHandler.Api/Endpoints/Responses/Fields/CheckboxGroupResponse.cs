namespace FormHandler.Api.Endpoints.Responses.Fields
{
    public sealed class CheckboxGroupResponse : FieldResponse
    {
        public required IReadOnlyList<FieldOptionResponse> Options { get; init; }
    }
}
