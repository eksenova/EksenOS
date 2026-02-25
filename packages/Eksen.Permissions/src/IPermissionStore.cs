using Eksen.Entities.Users;

namespace Eksen.Permissions;

public interface IPermissionStore
{
    Task<List<PermissionDefinition>> GetPermissionDefinitionsAsync(
        CancellationToken cancellationToken = default);

    Task<List<PermissionDefinition>> GetUserPermissionsAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default);
}