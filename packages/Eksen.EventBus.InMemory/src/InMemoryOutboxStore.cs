using System.Collections.Concurrent;
using Eksen.EventBus.Outbox;

namespace Eksen.EventBus.InMemory;

public class InMemoryOutboxStore : IOutboxStore
{
    private readonly ConcurrentDictionary<Guid, OutboxMessage> _messages = new();

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
        }

        return Task.CompletedTask;
    }

    public Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var message))
        {
            message.Status = OutboxMessageStatus.Failed;
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
