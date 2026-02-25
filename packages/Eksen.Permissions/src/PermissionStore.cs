using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Entities.Users;

namespace Eksen.Permissions;

public class PermissionStore<TUser, TRole, TTenant>(
    IEksenUserPermissionRepository<TUser, TTenant> userPermissionRepository,
    IEksenRolePermissionRepository<TRole, TTenant> rolePermissionRepository,
    IEksenUserRoleRepository<TUser, TRole, TTenant> userRoleRepository,
    IPermissionDefinitionRepository permissionDefinitionRepository) : IPermissionStore
    where TUser : class, IEksenUser<TTenant>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    public virtual async Task<List<PermissionDefinition>> GetPermissionDefinitionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var permissions = await permissionDefinitionRepository.GetListAsync(
            x => !x.IsDisabled,
            cancellationToken: cancellationToken
        );

        return permissions
            .DistinctBy(x => x.Name)
            .ToList();
    }

    public virtual async Task<List<PermissionDefinition>> GetUserPermissionsAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default
    )
    {
        var userPermissions = await userPermissionRepository
            .GetByUserIdAsync(userId, cancellationToken);

        var roles = await userRoleRepository.GetRolesByUserIdAsync(userId, cancellationToken);
        var roleIds = roles.Select(x => x.Id).ToList();

        var rolePermissions = await rolePermissionRepository
            .GetByRoleIdsAsync(roleIds, cancellationToken);

        var permissions = rolePermissions
            .Union(userPermissions)
            .DistinctBy(x => x.Name)
            .Where(x => !x.IsDisabled)
            .ToList();

        return permissions;
    }
}