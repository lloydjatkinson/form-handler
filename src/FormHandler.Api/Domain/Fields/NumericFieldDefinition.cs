namespace FormHandler.Api.Domain.Fields
{
    public abstract class NumericFieldDefinition : FieldDefinition
    {
        public decimal? Min { get; init; }
        public decimal? Max { get; init; }
        public decimal? Step { get; init; }
    }
}