namespace FormHandler.Api.Application.Outbox
{
    public interface IFormSubmissionOutboxStore
    {
        Task<IReadOnlyCollection<PendingOutboxEntry>> ClaimPendingAsync(CancellationToken cancellationToken = default);

        Task MarkAsSentAsync(Guid outboxId, CancellationToken cancellationToken = default);

        Task<OutboxFailureResult> MarkAsFailedAsync(Guid outboxId, string error, CancellationToken cancellationToken = default);
    }
}