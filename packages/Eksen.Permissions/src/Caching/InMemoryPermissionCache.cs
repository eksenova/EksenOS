using System.Diagnostics.Metrics;
using Eksen.Identity;
using Eksen.Identity.Users;
using Microsoft.Extensions.Caching.Memory;

namespace Eksen.Permissions.Caching;

public class InMemoryPermissionCache(
    IAuthContext authContext,
    Lazy<IPermissionStore> permissionStore,
    IMemoryCache memoryCache,
    IMeterFactory? meterFactory
) : BasePermissionCache(authContext, permissionStore, meterFactory)
{
    public InMemoryPermissionCache(
        IAuthContext authContext,
        Lazy<IPermissionStore> permissionStore,
        IMemoryCache memoryCache)
        : this(authContext, permissionStore, memoryCache, meterFactory: null)
    {
    }

    private readonly IAuthContext _authContext = authContext;

    public override async ValueTask InvalidateForUserAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default)
    {
        var key = GetCacheKeyForUserPermissions(userId);
        await InvalidateForPermissionDefinitionsAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        memoryCache.Remove(key);
        RecordInvalidation(UserKeyKind);
    }

    protected override Task WriteCacheAsync<T>(
        string cacheKey,
        T value,
        PermissionCacheEntryOptions options,
        CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        memoryCache.Set<T>(cacheKey, value, options);
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

        RecordInvalidation(UserKeyKind, userIds.Count);
        return ValueTask.CompletedTask;
    }

    public override ValueTask InvalidateForPermissionDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKeyForPermissionDefinitions();
        cancellationToken.ThrowIfCancellationRequested();

        memoryCache.Remove(cacheKey);
        RecordInvalidation(DefinitionsKeyKind);
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
        RecordInvalidation(UserKeyKind);
    }
}