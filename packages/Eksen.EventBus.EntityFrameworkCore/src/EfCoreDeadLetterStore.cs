using Eksen.EventBus.DeadLetter;
using Microsoft.EntityFrameworkCore;

namespace Eksen.EventBus.EntityFrameworkCore;

/// <summary>
/// Stores dead letters in the <c>EventBusDeadLetterMessages</c> table. <see cref="RequeueAsync"/> redelivers a dead
/// letter to the handler that failed it through the registered <see cref="IEventRescheduler"/>.
/// </summary>
public class EfCoreDeadLetterStore(
    EventBusDbContext dbContext,
    IEventRescheduler? rescheduler
) : IDeadLetterStore
{
    public EfCoreDeadLetterStore(EventBusDbContext dbContext)
        : this(dbContext, rescheduler: null)
    {
    }

    public async Task SaveAsync(DeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        var entity = new DeadLetterMessageEntity
        {
            Id = message.Id,
            OriginalMessageId = message.OriginalMessageId,
            EventType = message.EventType,
            HandlerType = message.HandlerType,
            Payload = message.Payload,
            CreationTime = message.CreationTime,
            FailedTime = message.FailedTime,
            TotalRetryCount = message.TotalRetryCount,
            LastError = message.LastError,
            CorrelationId = message.CorrelationId,
            SourceApp = message.SourceApp,
            TargetApp = message.TargetApp,
            IsRequeued = message.IsRequeued,
            RequeuedTime = message.RequeuedTime
        };

        dbContext.DeadLetterMessages.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeadLetterMessage>> GetMessagesAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.DeadLetterMessages
            .OrderByDescending(m => m.FailedTime)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDeadLetterMessage).ToList();
    }

    public async Task<DeadLetterMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.DeadLetterMessages.FindAsync([id], cancellationToken);
        return entity != null ? MapToDeadLetterMessage(entity) : null;
    }

    /// <summary>
    /// Redelivers the dead letter to its original handler queue only (with a fresh retry budget) and marks it
    /// requeued. A dead letter that is already requeued, or does not exist, is left alone.
    /// </summary>
    public async Task RequeueAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.DeadLetterMessages.AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == messageId, cancellationToken);
        if (entity == null || entity.IsRequeued)
        {
            return;
        }

        if (rescheduler == null)
        {
            throw new InvalidOperationException(
                "Requeueing a dead letter needs a transport that redelivers to a single handler (IEventRescheduler); none is registered.");
        }

        if (entity.HandlerType == null)
        {
            throw new InvalidOperationException(
                $"Dead letter {messageId} has no handler type, so it cannot be redelivered to the handler that failed it.");
        }

        // Mark first so two concurrent requeues of the same dead letter redeliver it once.
        var requeuedTime = DateTime.UtcNow;
        var marked = await dbContext.DeadLetterMessages
            .Where(m => m.Id == messageId && !m.IsRequeued)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(m => m.IsRequeued, true)
                    .SetProperty(m => m.RequeuedTime, requeuedTime),
                cancellationToken);
        if (marked == 0)
        {
            return;
        }

        try
        {
            await rescheduler.RescheduleAsync(
                new EventRedelivery
                {
                    EventTypeName = entity.EventType,
                    Payload = entity.Payload,
                    EventId = entity.OriginalMessageId,
                    HandlerTypeName = entity.HandlerType,
                    CorrelationId = entity.CorrelationId,
                    SourceApp = entity.SourceApp,
                    TargetApp = entity.TargetApp
                },
                TimeSpan.Zero,
                cancellationToken);
        }
        catch
        {
            await dbContext.DeadLetterMessages
                .Where(m => m.Id == messageId)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(m => m.IsRequeued, false)
                        .SetProperty(m => m.RequeuedTime, (DateTime?)null),
                    CancellationToken.None);
            throw;
        }
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.DeadLetterMessages
            .Where(m => !m.IsRequeued)
            .CountAsync(cancellationToken);
    }

    private static DeadLetterMessage MapToDeadLetterMessage(DeadLetterMessageEntity entity)
    {
        return new DeadLetterMessage
        {
            Id = entity.Id,
            OriginalMessageId = entity.OriginalMessageId,
            EventType = entity.EventType,
            HandlerType = entity.HandlerType,
            Payload = entity.Payload,
            CreationTime = entity.CreationTime,
            FailedTime = entity.FailedTime,
            TotalRetryCount = entity.TotalRetryCount,
            LastError = entity.LastError,
            CorrelationId = entity.CorrelationId,
            SourceApp = entity.SourceApp,
            TargetApp = entity.TargetApp,
            IsRequeued = entity.IsRequeued,
            RequeuedTime = entity.RequeuedTime
        };
    }
}
