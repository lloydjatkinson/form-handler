namespace FormHandler.Api.Infrastructure.Forms
{
    public sealed class FormDescriptor
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public EmailOptionsDescriptor? EmailOptions { get; set; }
        public List<FieldDescriptor> Fields { get; set; } = [];
        public Dictionary<string, string> Attributes { get; set; } = [];
    }
}
