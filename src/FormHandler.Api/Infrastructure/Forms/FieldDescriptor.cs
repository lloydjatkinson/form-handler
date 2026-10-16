namespace FormHandler.Api.Infrastructure.Forms
{
    public abstract class FieldDescriptor
    {
        // The 'type' key selects the concrete descriptor via the deserializer's type
        // discriminator. It is also bound here so the key is consumed rather than
        // reported as an unmatched property; the mapper dispatches on the CLR type.
        public string? Type { get; set; }
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Label { get; set; }
        public bool IsRequired { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = [];
    }
}
