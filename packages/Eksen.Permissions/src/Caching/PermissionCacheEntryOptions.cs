using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace Eksen.Permissions.Caching;

public record PermissionCacheEntryOptions
{
    public DateTimeOffset? AbsoluteExpiration { get; set; }

    public static implicit operator DistributedCacheEntryOptions(PermissionCacheEntryOptions options)
    {
        return new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = options.AbsoluteExpiration
        };
    }

    public static implicit operator MemoryCacheEntryOptions(PermissionCacheEntryOptions options)
    {
        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = options.AbsoluteExpiration
        };
        return cacheEntryOptions;
    }
}