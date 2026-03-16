using System.Text.Json;
using Eksen.EventBus.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Eksen.EventBus.EntityFrameworkCore;

public class EfCoreOutboxStore(EventBusDbContext dbContext) : IOutboxStore
{
    public async Task SaveAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        var entity = new OutboxMessageEntity
        {
            Id = message.Id,
            EventType = message.EventType,
            Payload = message.Payload,
            CreationTime = message.CreationTime,
            ProcessedTime = message.ProcessedTime,
            Status = (int)message.Status,
            RetryCount = message.RetryCount,
            LastError = message.LastError,
            CorrelationId = message.CorrelationId,
            SourceApp = message.SourceApp,
            TargetApp = message.TargetApp,
            Headers = message.Headers != null ? JsonSerializer.Serialize(message.Headers) : null
        };

        dbContext.OutboxMessages.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<OutboxMessage?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.OutboxMessages.FindAsync([messageId], cancellationToken);
        return entity != null ? MapToOutboxMessage(entity) : null;
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.OutboxMessages
            .Where(m => m.Status == (int)OutboxMessageStatus.Pending)
            .OrderBy(m => m.CreationTime)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToOutboxMessage).ToList();
    }

    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        await dbContext.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(m => m.Status, (int)OutboxMessageStatus.Processed)
                    .SetProperty(m => m.ProcessedTime, DateTime.UtcNow),
                cancellationToken);
    }

    public async Task MarkAsFailedAsync(
        Guid messageId,
        string error,
        CancellationToken cancellationToken = default)
    {
        await dbContext.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(m => m.Status, (int)OutboxMessageStatus.Failed)
                    .SetProperty(m => m.LastError, error),
                cancellationToken);
    }

    public async Task IncrementRetryCountAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        await dbContext.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(m => m.RetryCount, m => m.RetryCount + 1),
                cancellationToken);
    }

    public async Task<OutboxMessageStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = await dbContext.OutboxMessages
            .GroupBy(_ => 1)
            .Select(g => new OutboxMessageStats
            {
                Pending = g.Count(m => m.Status == (int)OutboxMessageStatus.Pending),
                Processing = g.Count(m => m.Status == (int)OutboxMessageStatus.Processing),
                Processed = g.Count(m => m.Status == (int)OutboxMessageStatus.Processed),
                Failed = g.Count(m => m.Status == (int)OutboxMessageStatus.Failed)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return stats ?? new OutboxMessageStats();
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetMessagesAsync(
        OutboxMessageStatus? status = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.OutboxMessages.AsQueryable();

        if (status.HasValue)
            query = query.Where(m => m.Status == (int)status.Value);

        var entities = await query
            .OrderByDescending(m => m.CreationTime)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToOutboxMessage).ToList();
    }

    private static OutboxMessage MapToOutboxMessage(OutboxMessageEntity entity)
    {
        return new OutboxMessage
        {
            Id = entity.Id,
            EventType = entity.EventType,
            Payload = entity.Payload,
            CreationTime = entity.CreationTime,
            ProcessedTime = entity.ProcessedTime,
            Status = (OutboxMessageStatus)entity.Status,
            RetryCount = entity.RetryCount,
            LastError = entity.LastError,
            CorrelationId = entity.CorrelationId,
            SourceApp = entity.SourceApp,
            TargetApp = entity.TargetApp,
            Headers = entity.Headers != null
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(entity.Headers)
                : null
        };
    }
}
