using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Permissions.EntityFrameworkCore;

public class EfCoreEksenUserPermissionRepository<TDbContext, TUser, TTenant>(TDbContext dbContext)
    : EfCoreIdRepository<TDbContext, EksenUserPermission<TUser, TTenant>, EksenUserPermissionId, System.Ulid>(dbContext),
        IEksenUserPermissionRepository<TUser, TTenant>
    where TDbContext : EksenDbContext
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    public async Task<ICollection<PermissionDefinition>> GetByUserIdAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default)
    {
        var queryable = GetQueryable()
            .Where(x => x.User.Id == userId)
            .Select(x => x.PermissionDefinition);

        return await queryable.ToListAsync(cancellationToken);
    }
}