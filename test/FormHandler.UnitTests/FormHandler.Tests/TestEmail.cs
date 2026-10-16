using FormHandler.Api.Domain;

namespace FormHandler.Tests
{
    public static class TestEmail
    {
        public static EmailAddress Email(string value) => EmailAddress.TryParse(value).Result!;
    }
}
