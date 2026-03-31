namespace FormHandler.Api.Application.Email
{
    public sealed class ComposedEmail
    {
        public string Subject { get; }
        public string HtmlBody { get; }
        public string PlainTextBody { get; }
        public string? ReplyTo { get; }

        public ComposedEmail(string subject, string htmlBody, string plainTextBody, string? replyTo = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(subject);
            ArgumentException.ThrowIfNullOrWhiteSpace(htmlBody);
            ArgumentException.ThrowIfNullOrWhiteSpace(plainTextBody);

            this.Subject = subject;
            this.HtmlBody = htmlBody;
            this.PlainTextBody = plainTextBody;
            this.ReplyTo = replyTo;
        }
    }
}