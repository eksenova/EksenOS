using Eksen.Auditing.Entities;
using Eksen.Auditing.Repositories;
using Eksen.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Auditing.EntityFrameworkCore;

public class EfCoreAuditLogRepository<TDbContext>(TDbContext dbContext)
    : EfCoreIdRepository<TDbContext, AuditLog, AuditLogId, System.Ulid, AuditLogFilterParameters>(dbContext),
        IAuditLogRepository
    where TDbContext : EksenDbContext
{
    protected override IQueryable<AuditLog> ApplyQueryFilters(
        IQueryable<AuditLog> queryable,
        AuditLogFilterParameters? filterParameters = null)
    {
        queryable = base.ApplyQueryFilters(queryable, filterParameters);

        if (filterParameters == null)
            return queryable;

        if (filterParameters.UserId != null)
            queryable = queryable.Where(x => x.UserId == filterParameters.UserId);

        if (filterParameters.TenantId != null)
            queryable = queryable.Where(x => x.TenantId == filterParameters.TenantId);

        if (filterParameters.FromTime != null)
            queryable = queryable.Where(x => x.LogTime >= filterParameters.FromTime);

        if (filterParameters.ToTime != null)
            queryable = queryable.Where(x => x.LogTime <= filterParameters.ToTime);

        if (!string.IsNullOrWhiteSpace(filterParameters.CorrelationId))
            queryable = queryable.Where(x => x.CorrelationId == filterParameters.CorrelationId);

        return queryable;
    }

    protected override IQueryable<AuditLog> ApplyDefaultIncludes(IQueryable<AuditLog> queryable)
    {
        return queryable
            .Include(x => x.HttpRequest)
            .Include(x => x.Actions)
            .Include(x => x.EntityChanges)
            .ThenInclude(x => x.PropertyChanges);
    }
}
