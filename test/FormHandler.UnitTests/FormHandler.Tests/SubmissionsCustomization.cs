using AutoFixture;

namespace FormHandler.Tests
{
    public sealed class SubmissionsCustomization : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(new FieldValueSpecimenBuilder());
        }
    }
}
