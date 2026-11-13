using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eksen.Identity;
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Microsoft.Extensions.Options;

namespace Eksen.Permissions;

public sealed class EksenPermissionOptions
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
    IOptions<EksenPermissionOptions> permissionOptions,
    IAuthContext authContext,
    IMeterFactory? meterFactory
) : IPermissionChecker
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    private readonly PermissionsTelemetry _telemetry = PermissionsTelemetry.For(meterFactory);

    public PermissionChecker(
        IPermissionCache permissionCache,
        IEksenUserRepository<TUser, TTenant> userRepository,
        IOptions<EksenPermissionOptions> permissionOptions,
        IAuthContext authContext)
        : this(permissionCache, userRepository, permissionOptions, authContext, meterFactory: null)
    {
    }

    public Task<bool> HasPermissionsAsync(PermissionName[] permissions)
    {
        return MeasureAsync(() => CheckCurrentUserAsync(permissions));
    }

    public Task<bool> HasPermissionsAsync(EksenUserId userId, PermissionName[] permissions)
    {
        return MeasureAsync(() => CheckUserAsync(userId, permissions));
    }

    private async Task<bool> MeasureAsync(Func<Task<bool>> check)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var result = "error";
        try
        {
            var granted = await check();
            result = granted ? "granted" : "denied";
            return granted;
        }
        finally
        {
            _telemetry.CheckDuration.Record(
                Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                new KeyValuePair<string, object?>("eksen.permissions.result", result));
        }
    }

    private async Task<bool> CheckCurrentUserAsync(PermissionName[] permissions)
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
        if (!permissionNames.All(grantedPermissions.Contains))
        {
            return false;
        }

        var userId = authContext.User?.UserId;
        if (userId == null)
        {
            return false;
        }

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

        if (user.IsPasswordChangeRequired && !permissions.All(passwordChangeAllowedPermissions.Contains))
        {
            return false;
        }

        return true;
    }

    private async Task<bool> CheckUserAsync(EksenUserId userId, PermissionName[] permissions)
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

    public Task<bool> HasPermissionAsync(PermissionName permission)
    {
        return HasPermissionsAsync([permission]);
    }

    public Task<bool> HasPermissionAsync(EksenUserId userId, PermissionName permission)
    {
        return HasPermissionsAsync(userId, [permission]);
    }
}