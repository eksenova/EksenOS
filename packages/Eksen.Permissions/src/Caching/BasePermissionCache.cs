using Eksen.Identity;
using Eksen.Identity.Users;
using System.Diagnostics.Metrics;
using Eksen.Telemetry;

namespace Eksen.Permissions.Caching;

public abstract class BasePermissionCache(
    IAuthContext authContext,
    Lazy<IPermissionStore> permissionStore,
    IMeterFactory? meterFactory)
    : IPermissionCache
{
    private readonly PermissionsTelemetry _telemetry = PermissionsTelemetry.For(meterFactory);

    protected BasePermissionCache(
        IAuthContext authContext,
        Lazy<IPermissionStore> permissionStore)
        : this(authContext, permissionStore, meterFactory: null)
    {
    }

    public const int CacheHours = 7 * 24;

    public abstract ValueTask InvalidateForUserAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default);

    public abstract ValueTask InvalidateForCurrentUserAsync(
        CancellationToken cancellationToken = default);

    public virtual Task<IReadOnlyCollection<string>> GetPermissionsForCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = authContext.User?.UserId;
        if (userId == null)
        {
            return Task.FromResult<IReadOnlyCollection<string>>([]);
        }

        return GetPermissionsForUserAsync(userId, cancellationToken);
    }

    public virtual async Task<IReadOnlyCollection<PermissionDefinition>> GetPermissionDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var key = GetCacheKeyForPermissionDefinitions();

        var cachedPermissions = await ReadCacheAsync<PermissionsDefinitionsCache>(key);

        _telemetry.RecordCacheRequest(cachedPermissions != null, PermissionsTelemetry.DefinitionsKeyKind);
        if (cachedPermissions != null)
        {
            return cachedPermissions.Permissions;
        }

        var permissions = await LoadFromStoreAsync(
            PermissionsTelemetry.DefinitionsKeyKind,
            () => permissionStore.Value.GetPermissionDefinitionsAsync());
        permissions.Sort((x, y) => string.Compare(x.Name.Value, y.Name.Value, StringComparison.Ordinal));

        cachedPermissions = new PermissionsDefinitionsCache
        {
            Permissions = [.. permissions]
        };

        var cacheEntryOptions = new PermissionCacheEntryOptions
        {
            AbsoluteExpiration = DateTimeOffset.Now.AddHours(CacheHours)
        };

        await WriteCacheAsync(key, cachedPermissions, cacheEntryOptions, cancellationToken);
        return permissions;
    }

    public virtual async Task<IReadOnlyCollection<string>> GetPermissionsForUserAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKeyForUserPermissions(userId);

        var cachedPermissions = await ReadCacheAsync<GrantedPermissionsCache>(
            cacheKey,
            cancellationToken);

        var hit = cachedPermissions is { Permissions.Count: > 0 };
        _telemetry.RecordCacheRequest(hit, PermissionsTelemetry.UserKeyKind);
        if (hit)
        {
            return cachedPermissions!.Permissions;
        }

        var permissions = await LoadFromStoreAsync(
            PermissionsTelemetry.UserKeyKind,
            () => permissionStore.Value.GetUserPermissionsAsync(userId));
        var permissionNames = permissions.Select(x => x.Name.Value).ToList();
        permissionNames.Sort((x, y) => string.Compare(x, y, StringComparison.Ordinal));

        cachedPermissions = new GrantedPermissionsCache
        {
            Permissions = [.. permissionNames]
        };

        await WriteCacheAsync(cacheKey, cachedPermissions, new PermissionCacheEntryOptions
        {
            AbsoluteExpiration = DateTimeOffset.Now.AddHours(CacheHours)
        }, cancellationToken);

        return permissionNames;
    }

    /// <summary>
    /// Counts an invalidation in <c>eksen.permissions.cache.invalidations</c>. <paramref name="keyKind"/> is
    /// <c>definitions</c> or <c>user</c>.
    /// </summary>
    protected void RecordInvalidation(string keyKind, long count = 1)
    {
        _telemetry.RecordInvalidation(keyKind, count);
    }

    protected const string DefinitionsKeyKind = PermissionsTelemetry.DefinitionsKeyKind;

    protected const string UserKeyKind = PermissionsTelemetry.UserKeyKind;

    private async Task<List<PermissionDefinition>> LoadFromStoreAsync(
        string keyKind,
        Func<Task<List<PermissionDefinition>>> load)
    {
        using var activity = _telemetry.StartStoreLoad(keyKind);
        try
        {
            return await load();
        }
        catch (Exception exception)
        {
            activity.RecordException(exception);
            throw;
        }
    }

    public abstract ValueTask InvalidateForUserIdsAsync(
        ICollection<EksenUserId> userIds,
        CancellationToken cancellationToken);

    public abstract ValueTask InvalidateForPermissionDefinitionsAsync(
        CancellationToken cancellationToken = default);

    protected abstract Task<T?> ReadCacheAsync<T>(
        string cacheKey,
        CancellationToken cancellationToken = default
    ) where T : class;

    protected abstract Task WriteCacheAsync<T>(
        string cacheKey,
        T value,
        PermissionCacheEntryOptions options,
        CancellationToken cancellationToken = default
    ) where T : class;

    protected virtual string GetCacheKeyForPermissionDefinitions()
    {
        return "permissions";
    }

    protected virtual string GetCacheKeyForUserPermissions(
        EksenUserId userId)
    {
        return $"permissions;u={userId.Value.ToString()}";
    }
}