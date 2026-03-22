using Eksen.EntityFrameworkCore;
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Authentication.ApiKeys.Identity.EntityFrameworkCore;

public class EfCoreEksenUserApiKeyRepository<TDbContext, TUser, TTenant>(TDbContext dbContext)
    : EfCoreIdRepository<TDbContext, EksenUserApiKey<TUser, TTenant>, EksenUserApiKeyId, System.Ulid>(dbContext),
        IEksenUserApiKeyRepository<TUser, TTenant>
    where TDbContext : EksenDbContext
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    public async Task<EksenUserApiKey<TUser, TTenant>?> FindByKeyValueAsync(
        ApiKeyValue keyValue,
        CancellationToken cancellationToken = default)
    {
        return await GetQueryable()
            .Include(x => x.User)
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.KeyValue == keyValue, cancellationToken);
    }

    public async Task<ICollection<EksenUserApiKey<TUser, TTenant>>> GetByUserIdAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default)
    {
        return await GetQueryable()
            .Where(x => x.User.Id == userId)
            .ToListAsync(cancellationToken);
    }
}
