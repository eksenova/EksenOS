using Eksen.Auditing.Entities;
using Eksen.Auditing.Repositories;
using Eksen.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Auditing.EntityFrameworkCore;

public class EfCoreAuditLogPropertyChangeRepository<TDbContext>(TDbContext dbContext)
    : EfCoreIdRepository<TDbContext, AuditLogPropertyChange, AuditLogPropertyChangeId, System.Ulid,
        AuditLogPropertyChangeFilterParameters>(dbContext),
        IAuditLogPropertyChangeRepository
    where TDbContext : EksenDbContext
{
    public async Task<ICollection<AuditLogPropertyChange>> GetByEntityChangeIdAsync(
        AuditLogEntityChangeId entityChangeId,
        CancellationToken cancellationToken = default)
    {
        return await GetQueryable()
            .Where(x => x.EntityChangeId == entityChangeId)
            .ToListAsync(cancellationToken);
    }

    protected override IQueryable<AuditLogPropertyChange> ApplyQueryFilters(
        IQueryable<AuditLogPropertyChange> queryable,
        AuditLogPropertyChangeFilterParameters? filterParameters = null)
    {
        queryable = base.ApplyQueryFilters(queryable, filterParameters);

        if (filterParameters == null)
            return queryable;

        if (filterParameters.EntityChangeId != null)
            queryable = queryable.Where(x => x.EntityChangeId == filterParameters.EntityChangeId);

        if (!string.IsNullOrWhiteSpace(filterParameters.PropertyName))
            queryable = queryable.Where(x => x.PropertyName == filterParameters.PropertyName);

        return queryable;
    }
}
