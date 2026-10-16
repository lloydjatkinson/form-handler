using AutoFixture;
using AutoFixture.Xunit3;

namespace FormHandler.Tests
{
    public sealed class FormHandlerCustomization : CompositeCustomization
    {
        public FormHandlerCustomization()
            : base(
                new SubmissionsCustomization(),
                new FormDefinitionCustomization())
        { }
    }

    public sealed class FormHandlerAutoDataAttribute : AutoDataAttribute
    {
        public FormHandlerAutoDataAttribute()
            : base(() => new Fixture().Customize(new FormHandlerCustomization())) { }
    }
}
