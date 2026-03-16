namespace Eksen.EventBus.Inbox;

public interface IInboxStore
{
    Task<bool> ExistsAsync(
        Guid eventId,
        string handlerType,
        CancellationToken cancellationToken = default);

    Task SaveAsync(InboxMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);

    Task MarkAsFailedAsync(
        Guid messageId,
        string error,
        CancellationToken cancellationToken = default);

    Task IncrementRetryCountAsync(Guid messageId, CancellationToken cancellationToken = default);

    Task<InboxMessageStats> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InboxMessage>> GetMessagesAsync(
        InboxMessageStatus? status = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);
}
