using FormHandler.Api.Domain;
using Shouldly;

namespace FormHandler.Tests.Domain
{
    public sealed class EmailAddressTests
    {
        [Fact]
        public void ValidAddressParses()
        {
            var (result, error) = EmailAddress.TryParse("alice@example.com");

            error.ShouldBeNull();
            result!.Address.ShouldBe("alice@example.com");
            result.DisplayName.ShouldBeNull();
        }

        [Fact]
        public void DisplayNameIsParsed()
        {
            var (result, error) = EmailAddress.TryParse("Alice Smith <alice@example.com>");

            error.ShouldBeNull();
            result!.Address.ShouldBe("alice@example.com");
            result.DisplayName.ShouldBe("Alice Smith");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(".")]
        [InlineData("not-an-email")]
        public void InvalidAddressProducesError(string value)
        {
            var (result, error) = EmailAddress.TryParse(value);

            result.ShouldBeNull();
            error.ShouldNotBeNull();
        }
    }
}
