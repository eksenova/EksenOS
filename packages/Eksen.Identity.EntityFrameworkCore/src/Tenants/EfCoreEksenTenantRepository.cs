using Eksen.EntityFrameworkCore;
using Eksen.Identity.Tenants;

namespace Eksen.Identity.EntityFrameworkCore.Tenants;

public class EfCoreEksenTenantRepository<TDbContext, TTenant>(TDbContext dbContext)
    : EfCoreIdRepository<
            TDbContext,
            TTenant,
            EksenTenantId,
            System.Ulid,
            EksenTenantFilterParameters<TTenant>,
            EksenTenantIncludeOptions<TTenant>
        >(dbContext),
        IEksenTenantRepository<TTenant>
    where TDbContext : EksenDbContext
    where TTenant : class, IEksenTenant
{
    protected override IQueryable<TTenant> ApplyQueryFilters(
        IQueryable<TTenant> queryable,
        EksenTenantFilterParameters<TTenant>? filterParameters = null)
    {
        queryable = base.ApplyQueryFilters(queryable, filterParameters);

        if (filterParameters == null)
        {
            return queryable;
        }

        queryable = !string.IsNullOrEmpty(filterParameters.SearchFilter)
            ? queryable.Where(x => ((string)(object)x.Name).Contains(filterParameters.SearchFilter))
            : queryable;

        return queryable;
    }
}