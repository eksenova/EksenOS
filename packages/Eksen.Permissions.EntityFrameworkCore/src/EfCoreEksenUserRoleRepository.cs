using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Permissions.EntityFrameworkCore;

public abstract class EfCoreEksenUserRoleRepository<TDbContext, TUser, TRole, TTenant>(TDbContext dbContext)
    : EfCoreIdRepository<TDbContext, EksenUserRole<TUser, TRole, TTenant>, EksenUserRoleId, System.Ulid>(dbContext),
        IEksenUserRoleRepository<TUser, TRole, TTenant>
    where TDbContext : EksenDbContext
    where TUser : class, IEksenUser<TTenant>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    public async Task<ICollection<TRole>> GetRolesByUserIdAsync(EksenUserId userId, CancellationToken cancellationToken = default)
    {
        var queryable = GetQueryable()
            .Where(x => x.User.Id == userId)
            .Select(x => x.Role);

        return await queryable.ToListAsync(cancellationToken);
    }
}