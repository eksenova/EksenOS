using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.Identity.Abstractions;
using Microsoft.Extensions.Options;

namespace Eksen.Permissions;

public sealed class PermissionOptions
{
    public ICollection<DefinedPermission> Permissions
    {
        get { return field ??= new List<DefinedPermission>(); }
    }

    public ICollection<PermissionName> PasswordChangeAllowedPermissions
    {
        get { return field ??= new List<PermissionName>(); }
    }
}

public sealed class PermissionChecker<TUser, TTenant>(
    IPermissionCache permissionCache,
    IEksenUserRepository<TUser, TTenant> userRepository,
    IOptions<PermissionOptions> permissionOptions,
    IAuthContext authContext
) : IPermissionChecker
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    public async Task<bool> HasPermissionsAsync(PermissionName[] permissions)
    {
        var permissionDefinitions = await permissionCache.GetPermissionDefinitionsAsync();
        var permissionNames = permissions
            .Select(x => x.Value)
            .ToList();

        if (!permissionNames.Select(x => x.ToLowerInvariant())
                .All(permissionDefinitions.Select(x => x.Name.Value.ToLowerInvariant()).Contains))
        {
            throw new Exception(message: "Missing permission defintions");
        }

        var grantedPermissions = await permissionCache.GetPermissionsForCurrentUserAsync();
        return permissionNames.All(grantedPermissions.Contains);
    }

    public async Task<bool> HasPermissionsAsync(EksenUserId userId, PermissionName[] permissions)
    {
        var permissionDefinitions = await permissionCache.GetPermissionDefinitionsAsync();
        var permissionNames = permissions.Select(x => x.Value).ToList();

        if (!permissionNames.Select(x => x.ToLowerInvariant())
                .All(permissionDefinitions.Select(x => x.Name.Value.ToLowerInvariant()).Contains))
        {
            throw new Exception(message: "Missing permission defintions");
        }

        var grantedPermissions = await permissionCache.GetPermissionsForUserAsync(userId);
        return permissionNames.All(grantedPermissions.Contains);
    }

    public async Task<bool> HasPermissionAsync(PermissionName permission)
    {
        var isPermissionGranted = await HasPermissionsAsync([permission]);

        var userId = authContext.User?.UserId;
        var user = await userRepository.FindByIdAsync(
            userId,
            new EksenUserIncludeOptions<TUser, TTenant>
            {
                IncludeTenant = true
            },
            cancellationToken: CancellationToken.None);
        if (user == null)
        {
            return false;
        }

        if (user.Tenant?.IsActive == false)
        {
            return false;
        }

        if (!user.IsActive)
        {
            return false;
        }

        var passwordChangeAllowedPermissions = permissionOptions.Value.PasswordChangeAllowedPermissions;

        if (user.IsPasswordChangeRequired && !passwordChangeAllowedPermissions.Contains(permission))
        {
            return false;
        }

        return isPermissionGranted;
    }

    public Task<bool> HasPermissionAsync(EksenUserId userId, PermissionName permission)
    {
        return HasPermissionsAsync(userId, [permission]);
    }
}