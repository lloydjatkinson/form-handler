using System.ComponentModel.DataAnnotations;

namespace FormHandler.Api.Application.Outbox
{
    public sealed class OutboxOptions
    {
        [Range(1, 1000)]
        public int BatchSize { get; init; } = 10;

        public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(30);
    }
}
