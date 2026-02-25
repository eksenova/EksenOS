using Eksen.Entities.Users;
using Eksen.Identity.Abstractions;

namespace Eksen.Permissions.Caching;

public class NullPermissionCache(
    IAuthContext authContext,
    Lazy<IPermissionStore> permissionStore
) : BasePermissionCache(authContext, permissionStore)
{
    public override ValueTask InvalidateForUserAsync(EksenUserId userId, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public override ValueTask InvalidateForCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public override ValueTask InvalidateForUserIdsAsync(ICollection<EksenUserId> userIds, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    public override ValueTask InvalidateForPermissionDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    protected override Task<T?> ReadCacheAsync<T>(string cacheKey, CancellationToken cancellationToken = default) where T : class
    {
        return Task.FromResult<T?>(result: null);
    }

    protected override Task WriteCacheAsync<T>(
        string cacheKey,
        T value,
        PermissionCacheEntryOptions options,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}