namespace FormHandler.Api.Infrastructure.Forms
{
    public abstract class ChoiceFieldDescriptor : FieldDescriptor
    {
        public List<FieldOptionDescriptor> Options { get; set; } = [];
    }
}
