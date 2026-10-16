namespace FormHandler.Api.Infrastructure.Forms
{
    public sealed class FieldOptionDescriptor
    {
        public string? Value { get; set; }
        public string? Label { get; set; }
        public bool IsDisabled { get; set; }
    }
}
