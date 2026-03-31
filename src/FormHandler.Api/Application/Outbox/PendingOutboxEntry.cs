namespace FormHandler.Api.Application.Outbox
{
    public sealed record PendingOutboxEntry(
        Guid OutboxId,
        Guid SubmissionId,
        string Type,
        int AttemptCount);
}