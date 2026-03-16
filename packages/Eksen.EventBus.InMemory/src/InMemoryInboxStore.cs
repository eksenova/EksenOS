using System.Collections.Concurrent;
using Eksen.EventBus.Inbox;

namespace Eksen.EventBus.InMemory;

public class InMemoryInboxStore : IInboxStore
{
    private readonly ConcurrentDictionary<Guid, InboxMessage> _messages = new();

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
