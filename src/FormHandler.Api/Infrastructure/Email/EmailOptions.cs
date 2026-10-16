using System;

namespace FormHandler.Api.Infrastructure.Email
{
    public sealed class EmailOptions
    {
        public required EmailAddressOptions From { get; init; }
        public required SmtpOptions Smtp { get; init; }
    }

    public sealed class EmailAddressOptions
    {
        public required string Address { get; init; }
        public required string Name { get; init; }
    }

    public sealed class SmtpOptions
    {
        public required string Host { get; init; }
        public required int Port { get; init; }
        public required string Username { get; init; }
        public required string Password { get; init; }
        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    }
}
