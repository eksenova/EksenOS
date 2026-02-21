using Eksen.Entities.Users;

namespace Eksen.Permissions;

public interface IPermissionStore
{
    Task<List<PermissionDefinition>> GetPermissionDefinitionsAsync();

    Task<List<PermissionDefinition>> GetUserPermissionsAsync(EksenUserId userId);
}