using AutoFixture;

namespace FormHandler.Tests
{
    public sealed class FormDefinitionCustomization : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(new FieldDefinitionListSpecimenBuilder());
        }
    }
}
