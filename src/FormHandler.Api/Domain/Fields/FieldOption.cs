namespace FormHandler.Api.Domain.Fields
{
    public sealed class FieldOption
    {
        public string Value { get; }
        public string Label { get; }
        public bool IsDisabled { get; }

        public FieldOption(string value, string label, bool isDisabled = false)
        {
            this.Value = value;
            this.Label = label;
            this.IsDisabled = isDisabled;
        }
    }
}