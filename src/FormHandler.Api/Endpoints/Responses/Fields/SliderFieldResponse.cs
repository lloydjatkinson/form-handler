namespace FormHandler.Api.Endpoints.Responses.Fields
{
    public sealed class SliderFieldResponse : FieldResponse
    {
        public decimal? Min { get; init; }
        public decimal? Max { get; init; }
        public decimal? Step { get; init; }
    }
}