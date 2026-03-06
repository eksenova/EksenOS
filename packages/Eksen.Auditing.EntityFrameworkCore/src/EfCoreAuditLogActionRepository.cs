using Eksen.Auditing.Entities;
using Eksen.Auditing.Repositories;
using Eksen.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Auditing.EntityFrameworkCore;

public class EfCoreAuditLogActionRepository<TDbContext>(TDbContext dbContext)
    : EfCoreIdRepository<TDbContext, AuditLogAction, AuditLogActionId, System.Ulid, AuditLogActionFilterParameters>(
        dbContext),
        IAuditLogActionRepository
    where TDbContext : EksenDbContext
{
    public async Task<ICollection<AuditLogAction>> GetByAuditLogIdAsync(
        AuditLogId auditLogId,
        CancellationToken cancellationToken = default)
    {
        return await GetQueryable()
            .Where(x => x.AuditLogId == auditLogId)
            .OrderBy(x => x.LogTime)
            .ToListAsync(cancellationToken);
    }

    protected override IQueryable<AuditLogAction> ApplyQueryFilters(
        IQueryable<AuditLogAction> queryable,
        AuditLogActionFilterParameters? filterParameters = null)
    {
        queryable = base.ApplyQueryFilters(queryable, filterParameters);

        if (filterParameters == null)
            return queryable;

        if (filterParameters.AuditLogId != null)
            queryable = queryable.Where(x => x.AuditLogId == filterParameters.AuditLogId);

        if (!string.IsNullOrWhiteSpace(filterParameters.ServiceType))
            queryable = queryable.Where(x => x.ServiceType.Contains(filterParameters.ServiceType));

        if (!string.IsNullOrWhiteSpace(filterParameters.MethodName))
            queryable = queryable.Where(x => x.MethodName.Contains(filterParameters.MethodName));

        return queryable;
    }
}
