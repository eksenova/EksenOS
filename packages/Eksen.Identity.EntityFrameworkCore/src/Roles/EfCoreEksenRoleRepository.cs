using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Identity.EntityFrameworkCore.Roles;

public class EfCoreEksenRoleRepository<TDbContext, TRole, TTenant, TFilterParameters, TIncludeOptions>(TDbContext dbContext)
    : EfCoreIdRepository<TDbContext, TRole, EksenRoleId, System.Ulid, TFilterParameters, TIncludeOptions>(dbContext),
        IEksenRoleRepository<TRole, TTenant, TFilterParameters, TIncludeOptions>
    where TDbContext : EksenDbContext
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
    where TFilterParameters : EksenRoleFilterParameters<TRole, TTenant>, new()
    where TIncludeOptions : EksenRoleIncludeOptions<TRole, TTenant>, new()
{
    protected override IQueryable<TRole> ApplyIncludes(IQueryable<TRole> queryable, TIncludeOptions? includeOptions = default)
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


    protected override IQueryable<TRole> ApplyQueryFilters(IQueryable<TRole> queryable, TFilterParameters? filterParameters = default)
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