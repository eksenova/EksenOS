using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Identity.EntityFrameworkCore.Roles;

public class EfCoreEksenRoleRepository<TDbContext, TRole, TTenant>(TDbContext dbContext)
    : EfCoreIdRepository<
            TDbContext,
            TRole,
            EksenRoleId,
            System.Ulid,
            EksenRoleFilterParameters<TRole, TTenant>,
            EksenRoleIncludeOptions<TRole, TTenant>
        >(dbContext),
        IEksenRoleRepository<TRole, TTenant>
    where TDbContext : EksenDbContext
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    protected override IQueryable<TRole> ApplyIncludes(
        IQueryable<TRole> queryable,
        EksenRoleIncludeOptions<TRole, TTenant>? includeOptions = null)
    {
        queryable = base.ApplyIncludes(queryable, includeOptions);

        if (includeOptions == null)
        {
            return queryable;
        }

        queryable = includeOptions.IncludeTenant
            ? queryable.Include(x => x.Tenant)
            : queryable;

        return queryable;
    }


    protected override IQueryable<TRole> ApplyQueryFilters(
        IQueryable<TRole> queryable,
        EksenRoleFilterParameters<TRole, TTenant>? filterParameters = null)
    {
        queryable = base.ApplyQueryFilters(queryable, filterParameters);

        if (filterParameters == null)
        {
            return queryable;
        }

        queryable = !string.IsNullOrWhiteSpace(filterParameters.SearchFilter)
            ? queryable.Where(x => ((string)(object)x.Name).Contains(filterParameters.SearchFilter))
            : queryable;

        return queryable;
    }
}