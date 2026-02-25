using System.Text;
using System.Text.Json;
using Eksen.Entities.Users;
using Eksen.Identity.Abstractions;
using Microsoft.Extensions.Caching.Distributed;

namespace Eksen.Permissions.Caching;

public class DistributedPermissionCache(
    IAuthContext authContext,
    Lazy<IPermissionStore> permissionStore,
    IDistributedCache distributedCache
) : BasePermissionCache(authContext, permissionStore)
{
    private readonly IAuthContext _authContext = authContext;

    public override async ValueTask InvalidateForUserAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default)
    {
        var key = GetCacheKeyForUserPermissions(userId);
        await InvalidateForPermissionDefinitionsAsync(cancellationToken);

        await distributedCache.RemoveAsync(key);
    }

    protected override async Task WriteCacheAsync<T>(
        string cacheKey,
        T value,
        PermissionCacheEntryOptions options,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var json = JsonSerializer.Serialize(value, JsonSerializerOptions.Web);
        var bytes = Encoding.UTF8.GetBytes(json);

        await distributedCache.SetAsync(cacheKey, bytes, options, cancellationToken);
    }

    protected override async Task<T?> ReadCacheAsync<T>(
        string cacheKey,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var bytes = await distributedCache.GetAsync(cacheKey, cancellationToken);
        if (bytes == null)
        {
            return default;
        }

        var json = Encoding.UTF8.GetString(bytes);
        return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions.Web);
    }

    public override async ValueTask InvalidateForUserIdsAsync(
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

    public override async ValueTask InvalidateForPermissionDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKeyForPermissionDefinitions();
        await distributedCache.RemoveAsync(cacheKey, cancellationToken);
    }

    public override async ValueTask InvalidateForCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = _authContext.User?.UserId;
        if (userId == null)
        {
            return;
        }

        await InvalidateForPermissionDefinitionsAsync(cancellationToken);
        var key = GetCacheKeyForUserPermissions(userId);

        await distributedCache.RemoveAsync(key, cancellationToken);
    }
}