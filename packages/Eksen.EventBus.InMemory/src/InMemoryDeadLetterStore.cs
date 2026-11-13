using System.Collections.Concurrent;
using Eksen.EventBus.DeadLetter;

namespace Eksen.EventBus.InMemory;

/// <summary>
/// Keeps dead letters in memory. <see cref="RequeueAsync"/> redelivers to the failed handler through the registered
/// <see cref="IEventRescheduler"/>.
/// </summary>
public class InMemoryDeadLetterStore(IEventRescheduler? rescheduler) : IDeadLetterStore
{
    private readonly ConcurrentDictionary<Guid, DeadLetterMessage> _messages = new();
    private readonly Lock _requeueLock = new();

    public InMemoryDeadLetterStore()
        : this(rescheduler: null)
    {
    }

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

    public async Task RequeueAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        if (!_messages.TryGetValue(messageId, out var message))
        {
            return;
        }

        if (rescheduler == null)
        {
            throw new InvalidOperationException(
                "Requeueing a dead letter needs a transport that redelivers to a single handler (IEventRescheduler); none is registered.");
        }

        if (message.HandlerType == null)
        {
            throw new InvalidOperationException(
                $"Dead letter {messageId} has no handler type, so it cannot be redelivered to the handler that failed it.");
        }

        lock (_requeueLock)
        {
            if (message.IsRequeued)
            {
                return;
            }

            message.IsRequeued = true;
            message.RequeuedTime = DateTime.UtcNow;
        }

        try
        {
            await rescheduler.RescheduleAsync(
                new EventRedelivery
                {
                    EventTypeName = message.EventType,
                    Payload = message.Payload,
                    EventId = message.OriginalMessageId,
                    HandlerTypeName = message.HandlerType,
                    CorrelationId = message.CorrelationId,
                    SourceApp = message.SourceApp,
                    TargetApp = message.TargetApp
                },
                TimeSpan.Zero,
                cancellationToken);
        }
        catch
        {
            lock (_requeueLock)
            {
                message.IsRequeued = false;
                message.RequeuedTime = null;
            }

            throw;
        }
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_messages.Values.Count(m => !m.IsRequeued));
    }
}
