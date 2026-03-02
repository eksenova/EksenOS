using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Eksen.Repositories;

namespace Eksen.Permissions;

public interface IEksenUserPermissionRepository<TUser, TTenant>
    : IIdRepository<EksenUserPermission<TUser, TTenant>, EksenUserPermissionId, System.Ulid>
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    Task<ICollection<PermissionDefinition>> GetByUserIdAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default);
}