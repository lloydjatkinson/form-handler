namespace FormHandler.Api.Domain.Fields
{
    public abstract class ChoiceFieldDefinition : FieldDefinition
    {
        private IReadOnlyList<FieldOption> options = [];

        public required IReadOnlyList<FieldOption> Options
        {
            get => this.options;
            init
            {
                if (value.Count == 0)
                {
                    throw new ArgumentException("At least one option is required.", nameof(value));
                }

                this.options = value;
            }
        }
    }
}
