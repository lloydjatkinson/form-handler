namespace FormHandler.Api.Infrastructure.Forms
{
    public sealed class TextFieldDescriptor : FieldDescriptor
    {
        public int? MaxLength { get; set; }
        public string? Placeholder { get; set; }
    }
}
