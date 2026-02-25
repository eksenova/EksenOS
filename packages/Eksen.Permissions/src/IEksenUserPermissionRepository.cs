using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.Repositories;

namespace Eksen.Permissions;

public interface IEksenUserPermissionRepository<TUser, TTenant>
    : IIdRepository<UserPermission<TUser, TTenant>, UserPermissionId, System.Ulid>
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    Task<ICollection<PermissionDefinition>> GetByUserIdAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default);
}