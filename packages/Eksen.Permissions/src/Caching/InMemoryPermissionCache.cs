using System.Text;
using System.Text.Json;
using Eksen.Entities.Users;
using Eksen.Identity.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace Eksen.Permissions.Caching;

public class InMemoryPermissionCache(
    IAuthContext authContext,
    Lazy<IPermissionStore> permissionStore,
    IMemoryCache memoryCache
) : BasePermissionCache(authContext, permissionStore)
{
    private readonly IAuthContext _authContext = authContext;

    public override async ValueTask InvalidateForUserAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default)
    {
        var key = GetCacheKeyForUserPermissions(userId);
        await InvalidateForPermissionDefinitionsAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        memoryCache.Remove(key);
    }

    protected override Task WriteCacheAsync<T>(
        string cacheKey,
        T value,
        PermissionCacheEntryOptions options,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var json = JsonSerializer.Serialize(value, JsonSerializerOptions.Web);
        var bytes = Encoding.UTF8.GetBytes(json);

        cancellationToken.ThrowIfCancellationRequested();
        memoryCache.Set(cacheKey, bytes, options);
        return Task.CompletedTask;
    }

    protected override Task<T?> ReadCacheAsync<T>(
        string cacheKey,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<T?>(memoryCache.Get<T>(cacheKey));
    }

    public override ValueTask InvalidateForUserIdsAsync(
        ICollection<EksenUserId> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        foreach (var userId in userIds)
        {
            var cacheKey = GetCacheKeyForUserPermissions(userId);

            cancellationToken.ThrowIfCancellationRequested();
            memoryCache.Remove(cacheKey);
        }

        return ValueTask.CompletedTask;
    }

    public override ValueTask InvalidateForPermissionDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKeyForPermissionDefinitions();
        cancellationToken.ThrowIfCancellationRequested();

        memoryCache.Remove(cacheKey);
        return ValueTask.CompletedTask;
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

        cancellationToken.ThrowIfCancellationRequested();
        memoryCache.Remove(key);
    }
}