namespace Eksen.EventBus.Inbox;

public interface IInboxStore
{
    /// <summary>
    /// True when the store writes through the ambient unit of work, so a rollback undoes its claim. A store that
    /// does not (Redis, in-memory) has its claim released explicitly when the handler fails.
    /// </summary>
    bool EnlistsInUnitOfWork
    {
        get { return true; }
    }

    Task<bool> ExistsAsync(
        Guid eventId,
        string handlerType,
        CancellationToken cancellationToken = default);

    async Task<InboxMessage?> FindAsync(
        Guid eventId, string handlerType, CancellationToken cancellationToken = default)
    {
        const int batchSize = 100;
        for (var skip = 0; ; skip += batchSize)
        {
            var messages = await GetMessagesAsync(null, skip, batchSize, cancellationToken);
            var match = messages.FirstOrDefault(message => message.EventId == eventId && message.HandlerType == handlerType);
            if (match != null || messages.Count < batchSize)
            {
                return match;
            }
        }
    }

    /// <summary>
    /// Claims <paramref name="message"/>'s event for its handler. The default implementation records the message
    /// (or re-opens a failed record) through the other members; stores with an atomic primitive override it.
    /// </summary>
    async Task<InboxClaim> TryClaimAsync(InboxMessage message, CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(message.EventId, message.HandlerType, cancellationToken);
        if (existing?.Status == InboxMessageStatus.Processed)
        {
            return new InboxClaim(InboxClaimStatus.AlreadyProcessed, existing);
        }

        if (existing != null)
        {
            return await TryMarkAsProcessingAsync(existing.Id, cancellationToken)
                ? new InboxClaim(InboxClaimStatus.Claimed, existing)
                : new InboxClaim(InboxClaimStatus.AlreadyProcessed, existing);
        }

        await SaveAsync(message, cancellationToken);
        return new InboxClaim(InboxClaimStatus.Claimed, message);
    }

    /// <summary>
    /// Records a claimed message as processed.
    /// </summary>
    Task CompleteAsync(InboxMessage message, CancellationToken cancellationToken = default)
    {
        return MarkAsProcessedAsync(message.Id, cancellationToken);
    }

    /// <summary>
    /// Gives up a claim after the handler failed, so a redelivery can claim the event again.
    /// </summary>
    Task ReleaseAsync(InboxMessage message, string error, CancellationToken cancellationToken = default)
    {
        return MarkAsFailedAsync(message.Id, error, cancellationToken);
    }

    /// <summary>
    /// Deletes up to <paramref name="batchSize"/> processed records whose processing finished before
    /// <paramref name="processedBefore"/>, returning how many were deleted.
    /// </summary>
    Task<int> DeleteProcessedAsync(
        DateTime processedBefore,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<bool> TryMarkAsProcessingAsync(Guid messageId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This inbox store does not support retrying existing messages.");

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
