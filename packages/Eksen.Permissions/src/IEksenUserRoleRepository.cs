using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.Repositories;

namespace Eksen.Permissions;

public interface IEksenUserRoleRepository<TUser, TRole, TTenant>
    : IIdRepository<EksenUserRole<TUser, TRole, TTenant>, EksenUserRoleId, System.Ulid>
    where TUser : class, IEksenUser<TTenant>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    Task<ICollection<TRole>> GetRolesByUserIdAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default);
}