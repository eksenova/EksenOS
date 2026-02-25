using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Repositories;

namespace Eksen.Permissions;

public interface IEksenRolePermissionRepository<TRole, TTenant>
    : IIdRepository<EksenRolePermission<TRole, TTenant>, EksenRolePermissionId, System.Ulid>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    Task<ICollection<PermissionDefinition>> GetByRoleIdAsync(
        EksenRoleId roleId,
        CancellationToken cancellationToken = default);

    Task<ICollection<PermissionDefinition>> GetByRoleIdsAsync(
        ICollection<EksenRoleId> roleIds,
        CancellationToken cancellationToken = default);
}