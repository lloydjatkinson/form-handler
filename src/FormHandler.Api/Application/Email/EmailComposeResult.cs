namespace FormHandler.Api.Application.Email
{
    public abstract record EmailComposeResult
    {
        private EmailComposeResult() { }

        public sealed record Composed(ComposedEmail Email) : EmailComposeResult;

        public sealed record Failed : EmailComposeResult
        {
            public string Reason { get; init; }
            public Exception? Exception { get; init; }

            public Failed(string reason)
            {
                this.Reason = reason;
            }

            public Failed(Exception exception)
            {
                this.Reason = exception.Message;
                this.Exception = exception;
            }
        }
    }
}