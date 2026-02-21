using Eksen.Entities.Users;

namespace Eksen.Permissions;

public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(PermissionName permission);

    Task<bool> HasPermissionAsync(EksenUserId userId, PermissionName permission);

    Task<bool> HasPermissionsAsync(PermissionName[] permissions);

    Task<bool> HasPermissionsAsync(EksenUserId userId, PermissionName[] permissions);
}