using Eksen.Entities.Tenants;
using Eksen.EntityFrameworkCore;
using Eksen.Repositories;

namespace Eksen.Identity.EntityFrameworkCore.Tenants;

public class EfCoreEksenTenantRepository<TDbContext, TTenant, TFilterParameters, TIncludeOptions>(TDbContext dbContext)
    : EfCoreIdRepository<TDbContext, TTenant, EksenTenantId, System.Ulid, TFilterParameters, TIncludeOptions>(dbContext),
        IEksenTenantRepository<TTenant, TFilterParameters, TIncludeOptions>,
        IEksenTenantRepository<TTenant>
    where TDbContext : EksenDbContext
    where TTenant : class, IEksenTenant
    where TFilterParameters : EksenTenantFilterParameters<TTenant>, new()
    where TIncludeOptions : EksenTenantIncludeOptions<TTenant>, new()
{
    protected override IQueryable<TTenant> ApplyQueryFilters(IQueryable<TTenant> queryable, TFilterParameters? filterParameters = default)
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

    public Task<TTenant?> FindAsync(
        EksenTenantFilterParameters<TTenant> filterParameters,
        EksenTenantIncludeOptions<TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        return base.FindAsync(
            (TFilterParameters)filterParameters,
            (TIncludeOptions?)includeOptions,
            queryOptions,
            cancellationToken);
    }

    public Task<TTenant> GetAsync(
        EksenTenantFilterParameters<TTenant> filterParameters,
        EksenTenantIncludeOptions<TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetAsync(
            (TFilterParameters)filterParameters,
            (TIncludeOptions?)includeOptions,
            queryOptions,
            cancellationToken);
    }

    public Task<ICollection<TTenant>> GetListAsync(
        EksenTenantFilterParameters<TTenant>? filterParameters = null,
        EksenTenantIncludeOptions<TTenant>? includeOptions = null,
        DefaultPaginationParameters? paginationParameters = null,
        DefaultSortingParameters<TTenant>? sortingParameters = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetListAsync(
            (TFilterParameters?)filterParameters,
            (TIncludeOptions?)includeOptions,
            paginationParameters,
            sortingParameters,
            queryOptions,
            cancellationToken);
    }

    public Task<long> CountAsync(
        EksenTenantFilterParameters<TTenant>? filterParameters = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        return base.CountAsync(
            (TFilterParameters?)filterParameters,
            queryOptions,
            cancellationToken);
    }

    public Task<TTenant?> FindAsync(
        EksenTenantId id,
        EksenTenantFilterParameters<TTenant>? filterParameters = null,
        EksenTenantIncludeOptions<TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        return base.FindAsync(
            id,
            (TFilterParameters?)filterParameters,
            (TIncludeOptions?)includeOptions,
            queryOptions,
            cancellationToken);
    }

    public Task<TTenant> GetAsync(
        EksenTenantId id,
        EksenTenantFilterParameters<TTenant>? filterParameters = null,
        EksenTenantIncludeOptions<TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetAsync(
            id,
            (TFilterParameters?)filterParameters,
            (TIncludeOptions?)includeOptions,
            queryOptions,
            cancellationToken);
    }

    public Task RemoveAsync(
        EksenTenantFilterParameters<TTenant> predicate,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        return base.RemoveAsync(
            (TFilterParameters)predicate,
            autoSave,
            cancellationToken);
    }
}