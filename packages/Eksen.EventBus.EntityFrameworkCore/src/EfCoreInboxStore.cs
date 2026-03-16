using Eksen.EventBus.Inbox;
using Microsoft.EntityFrameworkCore;

namespace Eksen.EventBus.EntityFrameworkCore;

public class EfCoreInboxStore(EventBusDbContext dbContext) : IInboxStore
{
    public async Task<bool> ExistsAsync(
        Guid eventId,
        string handlerType,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.InboxMessages
            .AnyAsync(
                m => m.EventId == eventId && m.HandlerType == handlerType,
                cancellationToken);
    }

    public async Task SaveAsync(InboxMessage message, CancellationToken cancellationToken = default)
    {
        var entity = new InboxMessageEntity
        {
            Id = message.Id,
            EventId = message.EventId,
            EventType = message.EventType,
            HandlerType = message.HandlerType,
            Payload = message.Payload,
            CreationTime = message.CreationTime,
            ProcessedTime = message.ProcessedTime,
            Status = (int)message.Status,
            RetryCount = message.RetryCount,
            LastError = message.LastError,
            CorrelationId = message.CorrelationId,
            SourceApp = message.SourceApp
        };

        dbContext.InboxMessages.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.InboxMessages
            .Where(m => m.Status == (int)InboxMessageStatus.Pending)
            .OrderBy(m => m.CreationTime)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToInboxMessage).ToList();
    }

    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        await dbContext.InboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(m => m.Status, (int)InboxMessageStatus.Processed)
                    .SetProperty(m => m.ProcessedTime, DateTime.UtcNow),
                cancellationToken);
    }

    public async Task MarkAsFailedAsync(
        Guid messageId,
        string error,
        CancellationToken cancellationToken = default)
    {
        await dbContext.InboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(m => m.Status, (int)InboxMessageStatus.Failed)
                    .SetProperty(m => m.LastError, error),
                cancellationToken);
    }

    public async Task IncrementRetryCountAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        await dbContext.InboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(m => m.RetryCount, m => m.RetryCount + 1),
                cancellationToken);
    }

    public async Task<InboxMessageStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = await dbContext.InboxMessages
            .GroupBy(_ => 1)
            .Select(g => new InboxMessageStats
            {
                Pending = g.Count(m => m.Status == (int)InboxMessageStatus.Pending),
                Processing = g.Count(m => m.Status == (int)InboxMessageStatus.Processing),
                Processed = g.Count(m => m.Status == (int)InboxMessageStatus.Processed),
                Failed = g.Count(m => m.Status == (int)InboxMessageStatus.Failed)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return stats ?? new InboxMessageStats();
    }

    public async Task<IReadOnlyList<InboxMessage>> GetMessagesAsync(
        InboxMessageStatus? status = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.InboxMessages.AsQueryable();

        if (status.HasValue)
            query = query.Where(m => m.Status == (int)status.Value);

        var entities = await query
            .OrderByDescending(m => m.CreationTime)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToInboxMessage).ToList();
    }

    private static InboxMessage MapToInboxMessage(InboxMessageEntity entity)
    {
        return new InboxMessage
        {
            Id = entity.Id,
            EventId = entity.EventId,
            EventType = entity.EventType,
            HandlerType = entity.HandlerType,
            Payload = entity.Payload,
            CreationTime = entity.CreationTime,
            ProcessedTime = entity.ProcessedTime,
            Status = (InboxMessageStatus)entity.Status,
            RetryCount = entity.RetryCount,
            LastError = entity.LastError,
            CorrelationId = entity.CorrelationId,
            SourceApp = entity.SourceApp
        };
    }
}
