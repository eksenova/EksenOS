using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.EntityFrameworkCore;
using Eksen.Repositories;
using Eksen.ValueObjects.Emailing;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Identity.EntityFrameworkCore.Users;

public class EfCoreEksenUserRepository<TDbContext, TUser, TTenant>(TDbContext dbContext)
    : EfCoreIdRepository<
            TDbContext,
            TUser,
            EksenUserId,
            System.Ulid,
            EksenUserFilterParameters<TUser, TTenant>,
            EksenUserIncludeOptions<TUser, TTenant>
        >(dbContext),
        IEksenUserRepository<TUser, TTenant>
    where TDbContext : EksenDbContext
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    public virtual Task<TUser?> FindByEmailAddressAsync(
        EmailAddress emailAddress,
        EksenUserIncludeOptions<TUser, TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        var queryable = GetQueryable(queryOptions);

        queryable = ApplyIncludes(queryable, includeOptions);

        return queryable.FirstOrDefaultAsync(x => x.EmailAddress == emailAddress, cancellationToken);
    }

    public virtual Task<TUser?> FindByIdAsync(
        EksenUserId? userId,
        EksenUserIncludeOptions<TUser, TTenant>? includeOptions = null,
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
        EksenUserIncludeOptions<TUser, TTenant>? includeOptions = null)
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
        EksenUserFilterParameters<TUser, TTenant>? filterParameters = null)
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
}