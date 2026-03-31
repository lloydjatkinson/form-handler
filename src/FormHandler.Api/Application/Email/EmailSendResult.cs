namespace FormHandler.Api.Application.Email
{
    public abstract record EmailSendResult
    {
        private EmailSendResult() { }

        public sealed record Sent : EmailSendResult;

        public sealed record Failed : EmailSendResult
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