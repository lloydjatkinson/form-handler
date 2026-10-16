using System.Net.Mail;

namespace FormHandler.Api.Domain
{
    public sealed class EmailAddress
    {
        public string Address { get; }
        public string? DisplayName { get; }

        private EmailAddress(string address, string? displayName)
        {
            this.Address = address;
            this.DisplayName = displayName;
        }

        public static (EmailAddress? Result, string? Error) TryParse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return (null, "must not be blank");
            }

            if (!MailAddress.TryCreate(value, out var parsed))
            {
                return (null, "must be a valid email address");
            }

            var displayName = string.IsNullOrWhiteSpace(parsed.DisplayName) ? null : parsed.DisplayName;
            return (new EmailAddress(parsed.Address, displayName), null);
        }
    }
}
