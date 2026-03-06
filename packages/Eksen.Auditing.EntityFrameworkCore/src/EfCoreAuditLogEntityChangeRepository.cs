using Eksen.Auditing.Entities;
using Eksen.Auditing.Repositories;
using Eksen.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Auditing.EntityFrameworkCore;

public class EfCoreAuditLogEntityChangeRepository<TDbContext>(TDbContext dbContext)
    : EfCoreIdRepository<TDbContext, AuditLogEntityChange, AuditLogEntityChangeId, System.Ulid,
        AuditLogEntityChangeFilterParameters>(dbContext),
        IAuditLogEntityChangeRepository
    where TDbContext : EksenDbContext
{
    public async Task<ICollection<AuditLogEntityChange>> GetByAuditLogIdAsync(
        AuditLogId auditLogId,
        CancellationToken cancellationToken = default)
    {
        return await GetQueryable()
            .Include(x => x.PropertyChanges)
            .Where(x => x.AuditLogId == auditLogId)
            .OrderBy(x => x.ChangeTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<ICollection<AuditLogEntityChange>> GetByEntityAsync(
        string entityTypeFullName,
        string? entityId = null,
        CancellationToken cancellationToken = default)
    {
        var queryable = GetQueryable()
            .Include(x => x.PropertyChanges)
            .Where(x => x.EntityTypeFullName == entityTypeFullName);

        if (!string.IsNullOrWhiteSpace(entityId))
            queryable = queryable.Where(x => x.EntityId == entityId);

        return await queryable
            .OrderByDescending(x => x.ChangeTime)
            .ToListAsync(cancellationToken);
    }

    protected override IQueryable<AuditLogEntityChange> ApplyQueryFilters(
        IQueryable<AuditLogEntityChange> queryable,
        AuditLogEntityChangeFilterParameters? filterParameters = null)
    {
        queryable = base.ApplyQueryFilters(queryable, filterParameters);

        if (filterParameters == null)
            return queryable;

        if (filterParameters.AuditLogId != null)
            queryable = queryable.Where(x => x.AuditLogId == filterParameters.AuditLogId);

        if (!string.IsNullOrWhiteSpace(filterParameters.EntityTypeFullName))
            queryable = queryable.Where(x => x.EntityTypeFullName == filterParameters.EntityTypeFullName);

        if (!string.IsNullOrWhiteSpace(filterParameters.EntityId))
            queryable = queryable.Where(x => x.EntityId == filterParameters.EntityId);

        if (filterParameters.ChangeType != null)
            queryable = queryable.Where(x => x.ChangeType == filterParameters.ChangeType);

        return queryable;
    }

    protected override IQueryable<AuditLogEntityChange> ApplyDefaultIncludes(
        IQueryable<AuditLogEntityChange> queryable)
    {
        return queryable.Include(x => x.PropertyChanges);
    }
}
