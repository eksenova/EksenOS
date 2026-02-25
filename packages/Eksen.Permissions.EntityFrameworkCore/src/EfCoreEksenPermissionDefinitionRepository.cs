using Eksen.EntityFrameworkCore;

namespace Eksen.Permissions.EntityFrameworkCore;

public class EfCoreEksenPermissionDefinitionRepository<TDbContext>(TDbContext dbContext)
    : EfCoreIdRepository<TDbContext, PermissionDefinition, PermissionDefinitionId, System.Ulid, PermissionFilterParameters>(dbContext),
        IEksenPermissionDefinitionRepository where TDbContext : EksenDbContext
{
    protected override IQueryable<PermissionDefinition> ApplyQueryFilters(
        IQueryable<PermissionDefinition> queryable,
        PermissionFilterParameters? filterParameters = null)
    {
        queryable = base.ApplyQueryFilters(queryable, filterParameters);

        if (filterParameters?.IsDisabled is not null)
        {
            queryable = queryable.Where(x => x.IsDisabled == filterParameters.IsDisabled);
        }

        return queryable;
    }
}