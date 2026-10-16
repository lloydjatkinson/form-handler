namespace FormHandler.Api.Infrastructure.Forms
{
    public sealed class EmailOptionsDescriptor
    {
        public string? To { get; set; }
        public List<string> Cc { get; set; } = [];
        public List<string> Bcc { get; set; } = [];
        public string? ReplyToFieldName { get; set; }
        public BrandingDescriptor? Branding { get; set; }
    }
}
