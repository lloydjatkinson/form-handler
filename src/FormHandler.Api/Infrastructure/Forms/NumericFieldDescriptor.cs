namespace FormHandler.Api.Infrastructure.Forms
{
    public abstract class NumericFieldDescriptor : FieldDescriptor
    {
        public decimal? Min { get; set; }
        public decimal? Max { get; set; }
        public decimal? Step { get; set; }
    }
}
