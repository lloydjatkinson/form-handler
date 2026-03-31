using System;
using System.Linq;
using FormHandler.Api.Domain.Fields;
using Shouldly;

namespace FormHandler.Tests.Domain.Fields
{
    public sealed class ChoiceFieldDefinitionTests
    {
        [Fact]
        public void SelectFieldEmptyOptionsThrows() =>
            Should.Throw<ArgumentException>(() => new SelectFieldDefinition { Id = "option-field", Name = "optionField", Label = "Option Field", Options = [] });

        [Fact]
        public void RadioGroupEmptyOptionsThrows() =>
            Should.Throw<ArgumentException>(() => new RadioGroupDefinition { Id = "option-field", Name = "optionField", Label = "Option Field", Options = [] });

        [Fact]
        public void CheckboxGroupEmptyOptionsThrows() =>
            Should.Throw<ArgumentException>(() => new CheckboxGroupDefinition { Id = "option-field", Name = "optionField", Label = "Option Field", Options = [] });

        [Fact]
        public void OptionsAreStoredInOrder()
        {
            var field = new RadioGroupDefinition
            {
                Id = "id",
                Name = "name",
                Label = "Label",
                Options = [new FieldOption("a", "Alpha"), new FieldOption("b", "Beta"), new FieldOption("c", "Gamma")]
            };

            field.Options.Select(o => o.Value).ShouldBe(["a", "b", "c"]);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsDisabledReflectsConstructorArgument(bool isDisabled)
        {
            var field = new SelectFieldDefinition
            {
                Id = "id",
                Name = "name",
                Label = "Label",
                Options = [new FieldOption("value", "Value", isDisabled: isDisabled)]
            };

            field.Options.ShouldHaveSingleItem().IsDisabled.ShouldBe(isDisabled);
        }
    }
}