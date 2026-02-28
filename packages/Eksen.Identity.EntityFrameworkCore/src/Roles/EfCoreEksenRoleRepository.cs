using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.EntityFrameworkCore;
using Eksen.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Identity.EntityFrameworkCore.Roles;

public class EfCoreEksenRoleRepository<TDbContext, TRole, TTenant, TFilterParameters, TIncludeOptions>(TDbContext dbContext)
    : EfCoreIdRepository<TDbContext, TRole, EksenRoleId, System.Ulid, TFilterParameters, TIncludeOptions>(dbContext),
        IEksenRoleRepository<TRole, TTenant, TFilterParameters, TIncludeOptions>,
        IEksenRoleRepository<TRole, TTenant>
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

    public Task<TRole?> FindAsync(
        EksenRoleFilterParameters<TRole, TTenant> filterParameters,
        EksenRoleIncludeOptions<TRole, TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        return base.FindAsync(
            (TFilterParameters)filterParameters,
            (TIncludeOptions?)includeOptions,
            queryOptions,
            cancellationToken
        );
    }

    public Task<TRole> GetAsync(
        EksenRoleFilterParameters<TRole, TTenant> filterParameters,
        EksenRoleIncludeOptions<TRole, TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetAsync(
            (TFilterParameters)filterParameters,
            (TIncludeOptions?)includeOptions,
            queryOptions,
            cancellationToken
        );
    }

    public Task<ICollection<TRole>> GetListAsync(
        EksenRoleFilterParameters<TRole, TTenant>? filterParameters = null,
        EksenRoleIncludeOptions<TRole, TTenant>? includeOptions = null,
        DefaultPaginationParameters? paginationParameters = null,
        DefaultSortingParameters<TRole>? sortingParameters = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetListAsync(
            (TFilterParameters?)filterParameters,
            (TIncludeOptions?)includeOptions,
            paginationParameters,
            sortingParameters,
            queryOptions,
            cancellationToken
        );
    }

    public Task<long> CountAsync(
        EksenRoleFilterParameters<TRole, TTenant>? filterParameters = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        return base.CountAsync(
            (TFilterParameters?)filterParameters,
            queryOptions,
            cancellationToken
        );
    }

    public Task<TRole?> FindAsync(
        EksenRoleId id,
        EksenRoleFilterParameters<TRole, TTenant>? filterParameters = null,
        EksenRoleIncludeOptions<TRole, TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        return base.FindAsync(
            id,
            (TFilterParameters?)filterParameters,
            (TIncludeOptions?)includeOptions,
            queryOptions,
            cancellationToken
        );
    }

    public Task<TRole> GetAsync(
        EksenRoleId id,
        EksenRoleFilterParameters<TRole, TTenant>? filterParameters = null,
        EksenRoleIncludeOptions<TRole, TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetAsync(
            id,
            (TFilterParameters?)filterParameters,
            (TIncludeOptions?)includeOptions,
            queryOptions,
            cancellationToken
        );
    }

    public Task RemoveAsync(
        EksenRoleFilterParameters<TRole, TTenant> predicate,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        return base.RemoveAsync(
            (TFilterParameters)predicate,
            autoSave,
            cancellationToken
        );
    }
}