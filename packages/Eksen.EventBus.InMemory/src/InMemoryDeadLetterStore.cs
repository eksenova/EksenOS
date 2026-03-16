using System.Collections.Concurrent;
using Eksen.EventBus.DeadLetter;

namespace Eksen.EventBus.InMemory;

public class InMemoryDeadLetterStore : IDeadLetterStore
{
    private readonly ConcurrentDictionary<Guid, DeadLetterMessage> _messages = new();

    public Task SaveAsync(DeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        _messages[message.Id] = message;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeadLetterMessage>> GetMessagesAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var result = _messages.Values
            .OrderByDescending(m => m.FailedTime)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult<IReadOnlyList<DeadLetterMessage>>(result);
    }

    public Task<DeadLetterMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _messages.TryGetValue(id, out var message);
        return Task.FromResult(message);
    }

    public Task RequeueAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var message))
        {
            message.IsRequeued = true;
            message.RequeuedTime = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_messages.Count);
    }
}
