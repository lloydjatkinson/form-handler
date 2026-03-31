namespace FormHandler.Api.Application.Outbox
{
    public abstract record OutboxFailureResult
    {
        public sealed record WillRetry(TimeSpan RetryIn) : OutboxFailureResult;
        public sealed record RetriesExhausted : OutboxFailureResult;
    }
}