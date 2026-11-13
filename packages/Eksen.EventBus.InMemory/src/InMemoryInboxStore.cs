using System.Collections.Concurrent;
using Eksen.EventBus.Inbox;

namespace Eksen.EventBus.InMemory;

public class InMemoryInboxStore : IInboxStore
{
    private readonly ConcurrentDictionary<Guid, InboxMessage> _messages = new();
    private readonly Lock _claimLock = new();

    public bool EnlistsInUnitOfWork
    {
        get { return false; }
    }

    public Task<InboxClaim> TryClaimAsync(InboxMessage message, CancellationToken cancellationToken = default)
    {
        lock (_claimLock)
        {
            var existing = _messages.Values
                .FirstOrDefault(m => m.EventId == message.EventId && m.HandlerType == message.HandlerType);
            if (existing == null)
            {
                _messages[message.Id] = message;
                return Task.FromResult(new InboxClaim(InboxClaimStatus.Claimed, message));
            }

            switch (existing.Status)
            {
                case InboxMessageStatus.Processed:
                {
                    return Task.FromResult(new InboxClaim(InboxClaimStatus.AlreadyProcessed, existing));
                }
                case InboxMessageStatus.Processing:
                {
                    return Task.FromResult(new InboxClaim(InboxClaimStatus.InProgress, existing));
                }
            }

            existing.Status = InboxMessageStatus.Processing;
            existing.LastError = null;
            existing.RetryCount++;
            return Task.FromResult(new InboxClaim(InboxClaimStatus.Claimed, existing));
        }
    }

    public Task<bool> TryMarkAsProcessingAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        lock (_claimLock)
        {
            if (!_messages.TryGetValue(messageId, out var message) || message.Status == InboxMessageStatus.Processed)
            {
                return Task.FromResult(false);
            }

            message.Status = InboxMessageStatus.Processing;
            message.LastError = null;
            message.RetryCount++;
            return Task.FromResult(true);
        }
    }

    public Task<int> DeleteProcessedAsync(
        DateTime processedBefore,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var expired = _messages.Values
            .Where(m => m.Status == InboxMessageStatus.Processed && m.ProcessedTime < processedBefore)
            .Take(batchSize)
            .Select(m => m.Id)
            .ToList();

        var deleted = expired.Count(id => _messages.TryRemove(id, out _));
        return Task.FromResult(deleted);
    }

    public Task<bool> ExistsAsync(
        Guid eventId,
        string handlerType,
        CancellationToken cancellationToken = default)
    {
        var exists = _messages.Values
            .Any(m => m.EventId == eventId && m.HandlerType == handlerType);

        return Task.FromResult(exists);
    }

    public Task SaveAsync(InboxMessage message, CancellationToken cancellationToken = default)
    {
        _messages[message.Id] = message;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<InboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var pending = _messages.Values
            .Where(m => m.Status == InboxMessageStatus.Pending)
            .OrderBy(m => m.CreationTime)
            .Take(batchSize)
            .ToList();

        return Task.FromResult<IReadOnlyList<InboxMessage>>(pending);
    }

    public Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var message))
        {
            message.Status = InboxMessageStatus.Processed;
            message.ProcessedTime = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var message))
        {
            message.Status = InboxMessageStatus.Failed;
            message.LastError = error;
        }

        return Task.CompletedTask;
    }

    public Task IncrementRetryCountAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var message))
        {
            message.RetryCount++;
        }

        return Task.CompletedTask;
    }

    public Task<InboxMessageStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new InboxMessageStats
        {
            Pending = _messages.Values.Count(m => m.Status == InboxMessageStatus.Pending),
            Processing = _messages.Values.Count(m => m.Status == InboxMessageStatus.Processing),
            Processed = _messages.Values.Count(m => m.Status == InboxMessageStatus.Processed),
            Failed = _messages.Values.Count(m => m.Status == InboxMessageStatus.Failed)
        };

        return Task.FromResult(stats);
    }

    public Task<IReadOnlyList<InboxMessage>> GetMessagesAsync(
        InboxMessageStatus? status = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _messages.Values.AsEnumerable();

        if (status.HasValue)
            query = query.Where(m => m.Status == status.Value);

        var result = query
            .OrderByDescending(m => m.CreationTime)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult<IReadOnlyList<InboxMessage>>(result);
    }
}
