using System.Text.Json;
using Eksen.EventBus.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Eksen.EventBus.EntityFrameworkCore;

public class EfCoreOutboxStore(EventBusDbContext dbContext) : IOutboxStore
{
    private const string NpgsqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    public async Task SaveAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        var entity = new OutboxMessageEntity
        {
            Id = message.Id,
            EventId = message.EventId,
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
            Headers = message.Headers != null ? JsonSerializer.Serialize(message.Headers) : null,
            NextAttemptTime = message.NextAttemptTime,
            LeaseToken = message.LeaseToken,
            LeaseExpiresAt = message.LeaseExpiresAt
        };

        dbContext.OutboxMessages.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<OutboxMessage?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        // Read through, not from the change tracker: the claim and status updates bypass tracked entities.
        var entity = await dbContext.OutboxMessages.AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == messageId, cancellationToken);
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

    public async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            return [];
        }

        var token = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var expiresAt = now + leaseDuration;

        if (dbContext.Database.ProviderName == NpgsqlProviderName)
        {
            await ClaimWithSkipLockedAsync(token, now, expiresAt, batchSize, cancellationToken);
        }
        else
        {
            await ClaimWithConditionalUpdateAsync(token, now, expiresAt, batchSize, cancellationToken);
        }

        var entities = await dbContext.OutboxMessages.AsNoTracking()
            .Where(m => m.LeaseToken == token)
            .OrderBy(m => m.CreationTime)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToOutboxMessage).ToList();
    }

    /// <summary>
    /// PostgreSQL: one statement locks the oldest due rows, skipping rows another publisher is claiming right now, and
    /// leases them.
    /// </summary>
    private async Task ClaimWithSkipLockedAsync(
        Guid token,
        DateTime now,
        DateTime expiresAt,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(OutboxMessageEntity))
            ?? throw new InvalidOperationException("The outbox entity is not part of the model.");
        var tableName = entityType.GetTableName()
            ?? throw new InvalidOperationException("The outbox entity is not mapped to a table.");
        var schema = entityType.GetSchema();
        var storeObject = StoreObjectIdentifier.Table(tableName, schema);
        var table = schema == null ? Quote(tableName) : $"{Quote(schema)}.{Quote(tableName)}";

        string Column(string propertyName)
        {
            var property = entityType.FindProperty(propertyName)
                ?? throw new InvalidOperationException($"The outbox entity has no {propertyName} property.");
            return Quote(property.GetColumnName(storeObject)
                ?? throw new InvalidOperationException($"The outbox {propertyName} property is not mapped."));
        }

        var id = Column(nameof(OutboxMessageEntity.Id));
        var status = Column(nameof(OutboxMessageEntity.Status));
        var nextAttemptTime = Column(nameof(OutboxMessageEntity.NextAttemptTime));
        var leaseToken = Column(nameof(OutboxMessageEntity.LeaseToken));
        var leaseExpiresAt = Column(nameof(OutboxMessageEntity.LeaseExpiresAt));
        var creationTime = Column(nameof(OutboxMessageEntity.CreationTime));

        var sql =
            $"UPDATE {table} SET {status} = {(int)OutboxMessageStatus.Processing}, {leaseToken} = {{0}}, {leaseExpiresAt} = {{1}} " +
            $"WHERE {id} IN (SELECT {id} FROM {table} " +
            $"WHERE ({status} = {(int)OutboxMessageStatus.Pending} AND ({nextAttemptTime} IS NULL OR {nextAttemptTime} <= {{2}})) " +
            $"OR ({status} = {(int)OutboxMessageStatus.Processing} AND ({leaseExpiresAt} IS NULL OR {leaseExpiresAt} <= {{2}})) " +
            $"ORDER BY {creationTime} LIMIT {{3}} FOR UPDATE SKIP LOCKED)";

        await dbContext.Database.ExecuteSqlRawAsync(sql, [token, expiresAt, now, batchSize], cancellationToken);
    }

    /// <summary>
    /// Other providers: the lease is taken by an update whose predicate re-checks that each candidate is still due,
    /// so of two concurrent publishers only one updates a row.
    /// </summary>
    private async Task ClaimWithConditionalUpdateAsync(
        Guid token,
        DateTime now,
        DateTime expiresAt,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var candidates = await dbContext.OutboxMessages.AsNoTracking()
            .Where(DueFilter(now))
            .OrderBy(m => m.CreationTime)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return;
        }

        await dbContext.OutboxMessages
            .Where(m => candidates.Contains(m.Id))
            .Where(DueFilter(now))
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(m => m.Status, (int)OutboxMessageStatus.Processing)
                    .SetProperty(m => m.LeaseToken, token)
                    .SetProperty(m => m.LeaseExpiresAt, expiresAt),
                cancellationToken);
    }

    private static System.Linq.Expressions.Expression<Func<OutboxMessageEntity, bool>> DueFilter(DateTime now)
    {
        return m =>
            (m.Status == (int)OutboxMessageStatus.Pending && (m.NextAttemptTime == null || m.NextAttemptTime <= now))
            || (m.Status == (int)OutboxMessageStatus.Processing && (m.LeaseExpiresAt == null || m.LeaseExpiresAt <= now));
    }

    private static string Quote(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }

    public async Task ScheduleRetryAsync(
        Guid messageId,
        DateTime nextAttemptTime,
        string error,
        CancellationToken cancellationToken = default)
    {
        await dbContext.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(m => m.Status, (int)OutboxMessageStatus.Pending)
                    .SetProperty(m => m.RetryCount, m => m.RetryCount + 1)
                    .SetProperty(m => m.LastError, error)
                    .SetProperty(m => m.NextAttemptTime, nextAttemptTime)
                    .SetProperty(m => m.LeaseToken, (Guid?)null)
                    .SetProperty(m => m.LeaseExpiresAt, (DateTime?)null),
                cancellationToken);
    }

    public async Task<int> DeleteProcessedAsync(
        DateTime processedBefore,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var ids = await dbContext.OutboxMessages.AsNoTracking()
            .Where(m => m.Status == (int)OutboxMessageStatus.Processed && m.ProcessedTime < processedBefore)
            .OrderBy(m => m.ProcessedTime)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            return 0;
        }

        return await dbContext.OutboxMessages
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        await dbContext.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(m => m.Status, (int)OutboxMessageStatus.Processed)
                    .SetProperty(m => m.ProcessedTime, DateTime.UtcNow)
                    .SetProperty(m => m.LeaseToken, (Guid?)null)
                    .SetProperty(m => m.LeaseExpiresAt, (DateTime?)null),
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
                    .SetProperty(m => m.LastError, error)
                    .SetProperty(m => m.LeaseToken, (Guid?)null)
                    .SetProperty(m => m.LeaseExpiresAt, (DateTime?)null),
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
            EventId = entity.EventId,
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
                : null,
            NextAttemptTime = entity.NextAttemptTime,
            LeaseToken = entity.LeaseToken,
            LeaseExpiresAt = entity.LeaseExpiresAt
        };
    }
}
