namespace FormHandler.Api.Infrastructure.Forms
{
    public sealed class TextAreaFieldDescriptor : FieldDescriptor
    {
        public int? Rows { get; set; }
        public int? Cols { get; set; }
    }
}
