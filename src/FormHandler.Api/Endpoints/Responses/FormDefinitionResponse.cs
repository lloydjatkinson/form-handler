using FormHandler.Api.Endpoints.Responses.Fields;

namespace FormHandler.Api.Endpoints.Responses
{
    public sealed class FormDefinitionResponse
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required IReadOnlyList<FieldResponse> Fields { get; init; }
    }
}
