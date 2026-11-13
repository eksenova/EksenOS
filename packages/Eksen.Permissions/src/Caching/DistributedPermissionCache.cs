using System.Text;
using System.Text.Json;
using Eksen.ValueObjects;
using System.Diagnostics.Metrics;
using Eksen.Identity;
using Eksen.Identity.Users;
using Microsoft.Extensions.Caching.Distributed;

namespace Eksen.Permissions.Caching;

public class DistributedPermissionCache(
    IAuthContext authContext,
    Lazy<IPermissionStore> permissionStore,
    IDistributedCache distributedCache,
    IMeterFactory? meterFactory
) : BasePermissionCache(authContext, permissionStore, meterFactory)
{
    public DistributedPermissionCache(
        IAuthContext authContext,
        Lazy<IPermissionStore> permissionStore,
        IDistributedCache distributedCache)
        : this(authContext, permissionStore, distributedCache, meterFactory: null)
    {
    }

    private readonly IAuthContext _authContext = authContext;
    private static readonly JsonSerializerOptions CacheJsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(JsonSerializerOptions.Default.GetConverter(typeof(string)));
        options.Converters.Add(JsonSerializerOptions.Default.GetConverter(typeof(System.Ulid)));
        options.Converters.Add(new JsonValueObjectConverter());
        return options;
    }

    protected override string GetCacheKeyForPermissionDefinitions()
        => "eksen:permissions:v2:definitions";

    protected override string GetCacheKeyForUserPermissions(EksenUserId userId)
        => $"eksen:permissions:v2:user:{userId.Value}";


    public override async ValueTask InvalidateForUserAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default)
    {
        var key = GetCacheKeyForUserPermissions(userId);
        await InvalidateForPermissionDefinitionsAsync(cancellationToken);

        await distributedCache.RemoveAsync(key);
        RecordInvalidation(UserKeyKind);
    }

    protected override async Task WriteCacheAsync<T>(
        string cacheKey,
        T value,
        PermissionCacheEntryOptions options,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var json = JsonSerializer.Serialize(value, CacheJsonOptions);
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
        return JsonSerializer.Deserialize<T>(json, CacheJsonOptions);
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

        RecordInvalidation(UserKeyKind, userIds.Count);
    }

    public override async ValueTask InvalidateForPermissionDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKeyForPermissionDefinitions();
        await distributedCache.RemoveAsync(cacheKey, cancellationToken);
        RecordInvalidation(DefinitionsKeyKind);
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
        RecordInvalidation(UserKeyKind);
    }
}