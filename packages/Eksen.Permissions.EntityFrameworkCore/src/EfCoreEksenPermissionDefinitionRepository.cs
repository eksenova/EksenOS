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

        if (filterParameters == null)
        {
            return queryable;
        }

        queryable = filterParameters.IsDisabled is not null
            ? queryable.Where(x => x.IsDisabled == filterParameters.IsDisabled)
            : queryable;

        queryable = !string.IsNullOrWhiteSpace(filterParameters.SearchFilter)
            ? queryable.Where(x => ((string)(object)x.Name).Contains(filterParameters.SearchFilter))
            : queryable;

        return queryable;
    }
}