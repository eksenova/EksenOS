using Eksen.EventBus.DeadLetter;
using Microsoft.EntityFrameworkCore;

namespace Eksen.EventBus.EntityFrameworkCore;

public class EfCoreDeadLetterStore(EventBusDbContext dbContext) : IDeadLetterStore
{
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

    public async Task RequeueAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        await dbContext.DeadLetterMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(m => m.IsRequeued, true)
                    .SetProperty(m => m.RequeuedTime, DateTime.UtcNow),
                cancellationToken);
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
