using System.Text;
using System.Text.Json;
using Eksen.Entities.Users;
using Eksen.Identity.Abstractions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace Eksen.Permissions;

public sealed class PermissionCache(
    IAuthContext authContext,
    IMemoryCache memoryCache,
    IDistributedCache distributedCache,
    Lazy<IPermissionStore> permissionStore)
    : IPermissionCache
{
    public const int CacheHours = 7 * 24;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async ValueTask InvalidateForUserAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default)
    {
        var key = GetCacheKeyForUserPermissions(userId);
        await InvalidateForPermissionDefinitionsAsync(cancellationToken);

        await distributedCache.RemoveAsync(key, cancellationToken);
    }

    public async ValueTask InvalidateForCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = authContext.User?.UserId;
        if (userId == null)
        {
            return;
        }

        await InvalidateForPermissionDefinitionsAsync(cancellationToken);
        var key = GetCacheKeyForUserPermissions(userId);

        await distributedCache.RemoveAsync(key, cancellationToken);
    }

    public Task<IReadOnlyCollection<string>> GetPermissionsForCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = authContext.User?.UserId;
        if (userId == null)
        {
            return Task.FromResult<IReadOnlyCollection<string>>([]);
        }

        return GetPermissionsForUserAsync(userId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<PermissionDefinition>> GetPermissionDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var key = GetCacheKeyForPermissionDefinitions();

        if (memoryCache.TryGetValue<PermissionsDefinitionsCache>(key, out var cachedPermissions)
            && cachedPermissions != null)
        {
            return cachedPermissions.Permissions;
        }

        var permissions = await permissionStore.Value.GetPermissionDefinitionsAsync();
        permissions.Sort((x, y) => string.Compare(x.Name.Value, y.Name.Value, StringComparison.Ordinal));

        cachedPermissions = new PermissionsDefinitionsCache
        {
            Permissions = [.. permissions]
        };

        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = DateTimeOffset.Now.AddHours(CacheHours)
        };

        memoryCache.Set(key, cachedPermissions, cacheEntryOptions);
        return permissions;
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionsForUserAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKeyForUserPermissions(userId);

        var cachedPermissions = await ReadCacheAsync<GrantedPermissionsCache>(
            cacheKey,
            cancellationToken);

        if (cachedPermissions is { Permissions.Count: > 0 })
        {
            return cachedPermissions.Permissions;
        }

        var permissions = await permissionStore.Value.GetUserPermissionsAsync(userId);
        var permissionNames = permissions.Select(x => x.Name.Value).ToList();
        permissionNames.Sort((x, y) => string.Compare(x, y, StringComparison.Ordinal));

        cachedPermissions = new GrantedPermissionsCache
        {
            Permissions = [.. permissionNames]
        };

        await WriteCacheAsync(cacheKey, cachedPermissions, new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = DateTimeOffset.Now.AddHours(CacheHours)
        }, cancellationToken);

        return permissionNames;
    }

    public async Task InvalidateForUserIdsAsync(
        ICollection<EksenUserId> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        foreach (var userId in userIds)
        {
            var cacheKey = GetCacheKeyForUserPermissions(userId);
            await distributedCache.RemoveAsync(cacheKey, cancellationToken);
        }
    }

    public ValueTask InvalidateForPermissionDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKeyForPermissionDefinitions();
        memoryCache.Remove(cacheKey);

        return ValueTask.CompletedTask;
    }

    private async Task<T?> ReadCacheAsync<T>(
        string cacheKey,
        CancellationToken cancellationToken = default)
    {
        var bytes = await distributedCache.GetAsync(cacheKey, cancellationToken);
        if (bytes == null)
        {
            return default;
        }

        var json = Encoding.UTF8.GetString(bytes);
        return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
    }

    private async Task WriteCacheAsync<T>(
        string cacheKey,
        T value,
        DistributedCacheEntryOptions options,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value, JsonSerializerOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        await distributedCache.SetAsync(cacheKey, bytes, options, cancellationToken);
    }

    private static string GetCacheKeyForPermissionDefinitions()
    {
        return "permissions";
    }

    private static string GetCacheKeyForUserPermissions(
        EksenUserId userId)
    {
        return $"permissions;u={userId.Value.ToString()}";
    }
}