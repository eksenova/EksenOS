using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.EntityFrameworkCore;
using Eksen.Repositories;
using Eksen.ValueObjects.Emailing;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Identity.EntityFrameworkCore.Users;

public class EfCoreEksenUserRepository<TDbContext, TUser, TTenant, TFilterParameters, TIncludeOptions>(TDbContext dbContext)
    : EfCoreIdRepository<TDbContext, TUser, EksenUserId, System.Ulid, TFilterParameters, TIncludeOptions>(dbContext),
        IEksenUserRepository<TUser, TTenant, TFilterParameters, TIncludeOptions>, IEksenUserRepository<TUser, TTenant>
    where TDbContext : EksenDbContext
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
    where TFilterParameters : EksenUserFilterParameters<TUser, TTenant>, new()
    where TIncludeOptions : EksenUserIncludeOptions<TUser, TTenant>, new()
{
    public virtual Task<TUser?> FindByEmailAddressAsync(
        EmailAddress emailAddress,
        TIncludeOptions? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        var queryable = GetQueryable(queryOptions);

        queryable = ApplyIncludes(queryable, includeOptions);

        return queryable.FirstOrDefaultAsync(x => x.EmailAddress == emailAddress, cancellationToken);
    }

    public virtual Task<TUser?> FindByIdAsync(
        EksenUserId? userId,
        TIncludeOptions? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        var queryable = GetQueryable(queryOptions);

        queryable = ApplyIncludes(queryable, includeOptions);

        return queryable.FirstOrDefaultAsync(
            x => x.Id == userId,
            cancellationToken);
    }

    protected override IQueryable<TUser> ApplyIncludes(
        IQueryable<TUser> queryable,
        TIncludeOptions? includeOptions = null)
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

    protected override IQueryable<TUser> ApplyQueryFilters(
        IQueryable<TUser> queryable,
        TFilterParameters? filterParameters = null)
    {
        queryable = base.ApplyQueryFilters(queryable, filterParameters);

        if (filterParameters == null)
        {
            return queryable;
        }

        queryable = !string.IsNullOrEmpty(filterParameters.SearchFilter)
            ? queryable
                .Where(x => ((string)(object)x.EmailAddress!)
                    .Contains(filterParameters.SearchFilter))
            : queryable;

        return queryable;
    }

    public Task<TUser?> FindAsync(
        EksenUserFilterParameters<TUser, TTenant> filterParameters,
        EksenUserIncludeOptions<TUser, TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        return base.FindAsync(
            (TFilterParameters)filterParameters,
            (TIncludeOptions?)includeOptions,
            queryOptions,
            cancellationToken);
    }

    public Task<TUser> GetAsync(
        EksenUserFilterParameters<TUser, TTenant> filterParameters,
        EksenUserIncludeOptions<TUser, TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetAsync(
            (TFilterParameters)filterParameters,
            (TIncludeOptions?)includeOptions,
            queryOptions,
            cancellationToken);
    }

    public Task<ICollection<TUser>> GetListAsync(
        EksenUserFilterParameters<TUser, TTenant>? filterParameters = null,
        EksenUserIncludeOptions<TUser, TTenant>? includeOptions = null,
        DefaultPaginationParameters? paginationParameters = null,
        DefaultSortingParameters<TUser>? sortingParameters = null,
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
        EksenUserFilterParameters<TUser, TTenant>? filterParameters = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        return base.CountAsync(
            (TFilterParameters?)filterParameters,
            queryOptions,
            cancellationToken);
    }

    public Task<TUser?> FindAsync(
        EksenUserId id,
        EksenUserFilterParameters<TUser, TTenant>? filterParameters = null,
        EksenUserIncludeOptions<TUser, TTenant>? includeOptions = null,
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

    public Task<TUser> GetAsync(
        EksenUserId id,
        EksenUserFilterParameters<TUser, TTenant>? filterParameters = null,
        EksenUserIncludeOptions<TUser, TTenant>? includeOptions = null,
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
        EksenUserFilterParameters<TUser, TTenant> predicate,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        return base.RemoveAsync(
            (TFilterParameters)predicate,
            autoSave,
            cancellationToken);
    }

    public Task<TUser?> FindByEmailAddressAsync(
        EmailAddress emailAddress,
        EksenUserIncludeOptions<TUser, TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        return FindByEmailAddressAsync(
            emailAddress,
            (TIncludeOptions?)includeOptions,
            queryOptions,
            cancellationToken);
    }

    public Task<TUser?> FindByIdAsync(
        EksenUserId? userId,
        EksenUserIncludeOptions<TUser, TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        return FindByIdAsync(
            userId,
            (TIncludeOptions?)includeOptions,
            queryOptions,
            cancellationToken);
    }
}