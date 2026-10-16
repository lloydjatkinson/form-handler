namespace FormHandler.Api.Domain.Fields
{
    public abstract class NumericFieldDefinition : FieldDefinition
    {
        private decimal? min;
        private decimal? max;

        public decimal? Min
        {
            get => this.min;
            init
            {
                if (value.HasValue && this.max.HasValue && value.Value > this.max.Value)
                {
                    throw new ArgumentException("Min must not be greater than Max.", nameof(value));
                }

                this.min = value;
            }
        }

        public decimal? Max
        {
            get => this.max;
            init
            {
                if (value.HasValue && this.min.HasValue && value.Value < this.min.Value)
                {
                    throw new ArgumentException("Max must not be less than Min.", nameof(value));
                }

                this.max = value;
            }
        }

        public decimal? Step { get; init; }
    }
}
