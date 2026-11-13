namespace Eksen.EventBus.Outbox;

public interface IOutboxStore
{
    Task SaveAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    Task<OutboxMessage?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pending rows in creation order, without claiming them. Publishers use <see cref="ClaimAsync"/>.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically leases up to <paramref name="batchSize"/> due rows to the caller: pending rows whose
    /// <see cref="OutboxMessage.NextAttemptTime"/> has passed, and rows whose previous lease expired. A row is leased
    /// to one caller at a time, so concurrent publishers never publish the same row together.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);

    Task MarkAsFailedAsync(
        Guid messageId,
        string error,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a failed publish attempt: increments the retry count, stores the error, releases the lease and makes
    /// the row due again at <paramref name="nextAttemptTime"/>.
    /// </summary>
    Task ScheduleRetryAsync(
        Guid messageId,
        DateTime nextAttemptTime,
        string error,
        CancellationToken cancellationToken = default);

    Task IncrementRetryCountAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes up to <paramref name="batchSize"/> processed rows published before <paramref name="processedBefore"/>,
    /// returning how many were deleted.
    /// </summary>
    Task<int> DeleteProcessedAsync(
        DateTime processedBefore,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<OutboxMessageStats> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxMessage>> GetMessagesAsync(
        OutboxMessageStatus? status = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);
}
