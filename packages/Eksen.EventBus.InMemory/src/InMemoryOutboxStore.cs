using System.Collections.Concurrent;
using Eksen.EventBus.Outbox;

namespace Eksen.EventBus.InMemory;

public class InMemoryOutboxStore : IOutboxStore
{
    private readonly ConcurrentDictionary<Guid, OutboxMessage> _messages = new();
    private readonly Lock _claimLock = new();

    public Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var token = Guid.NewGuid();
        lock (_claimLock)
        {
            var claimed = _messages.Values
                .Where(m =>
                    (m.Status == OutboxMessageStatus.Pending && (m.NextAttemptTime == null || m.NextAttemptTime <= now))
                    || (m.Status == OutboxMessageStatus.Processing && (m.LeaseExpiresAt == null || m.LeaseExpiresAt <= now)))
                .OrderBy(m => m.CreationTime)
                .Take(batchSize)
                .ToList();

            foreach (var message in claimed)
            {
                message.Status = OutboxMessageStatus.Processing;
                message.LeaseToken = token;
                message.LeaseExpiresAt = now + leaseDuration;
            }

            return Task.FromResult<IReadOnlyList<OutboxMessage>>(claimed);
        }
    }

    public Task ScheduleRetryAsync(
        Guid messageId,
        DateTime nextAttemptTime,
        string error,
        CancellationToken cancellationToken = default)
    {
        lock (_claimLock)
        {
            if (_messages.TryGetValue(messageId, out var message))
            {
                message.Status = OutboxMessageStatus.Pending;
                message.RetryCount++;
                message.LastError = error;
                message.NextAttemptTime = nextAttemptTime;
                message.LeaseToken = null;
                message.LeaseExpiresAt = null;
            }
        }

        return Task.CompletedTask;
    }

    public Task<int> DeleteProcessedAsync(
        DateTime processedBefore,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var expired = _messages.Values
            .Where(m => m.Status == OutboxMessageStatus.Processed && m.ProcessedTime < processedBefore)
            .Take(batchSize)
            .Select(m => m.Id)
            .ToList();

        var deleted = expired.Count(id => _messages.TryRemove(id, out _));
        return Task.FromResult(deleted);
    }

    public Task SaveAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        _messages[message.Id] = message;
        return Task.CompletedTask;
    }

    public Task<OutboxMessage?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        _messages.TryGetValue(messageId, out var message);
        return Task.FromResult(message);
    }

    public Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var pending = _messages.Values
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.CreationTime)
            .Take(batchSize)
            .ToList();

        return Task.FromResult<IReadOnlyList<OutboxMessage>>(pending);
    }

    public Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var message))
        {
            message.Status = OutboxMessageStatus.Processed;
            message.ProcessedTime = DateTime.UtcNow;
            message.LeaseToken = null;
            message.LeaseExpiresAt = null;
        }

        return Task.CompletedTask;
    }

    public Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var message))
        {
            message.Status = OutboxMessageStatus.Failed;
            message.LastError = error;
            message.LeaseToken = null;
            message.LeaseExpiresAt = null;
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

    public Task<OutboxMessageStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new OutboxMessageStats
        {
            Pending = _messages.Values.Count(m => m.Status == OutboxMessageStatus.Pending),
            Processing = _messages.Values.Count(m => m.Status == OutboxMessageStatus.Processing),
            Processed = _messages.Values.Count(m => m.Status == OutboxMessageStatus.Processed),
            Failed = _messages.Values.Count(m => m.Status == OutboxMessageStatus.Failed)
        };

        return Task.FromResult(stats);
    }

    public Task<IReadOnlyList<OutboxMessage>> GetMessagesAsync(
        OutboxMessageStatus? status = null,
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

        return Task.FromResult<IReadOnlyList<OutboxMessage>>(result);
    }
}
