using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.EntityFrameworkCore;
using Eksen.Repositories;
using Eksen.ValueObjects.Emailing;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Permissions.EntityFrameworkCore;

public abstract class EfCoreEksenUserRepository<TDbContext, TUser, TTenant, TFilterParameters, TIncludeOptions>(TDbContext dbContext)
    : EfCoreIdRepository<TDbContext, TUser, EksenUserId, System.Ulid, TFilterParameters, TIncludeOptions>(dbContext),
        IEksenUserRepository<TUser, TTenant, TFilterParameters, TIncludeOptions>
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

        if (includeOptions != null)
        {
            queryable = ApplyIncludes(queryable, includeOptions);
        }

        return queryable.FirstOrDefaultAsync(x => x.EmailAddress == emailAddress, cancellationToken);
    }

    public virtual Task<TUser?> FindByIdAsync(
        EksenUserId? userId,
        TIncludeOptions? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        var queryable = GetQueryable(queryOptions);

        if (includeOptions != null)
        {
            queryable = ApplyIncludes(queryable, includeOptions);
        }

        return queryable.FirstOrDefaultAsync(
            x => x.Id == userId,
            cancellationToken);
    }
}