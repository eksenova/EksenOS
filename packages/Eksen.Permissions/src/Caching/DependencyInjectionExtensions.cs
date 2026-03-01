using Eksen.Permissions;
using Eksen.Permissions.Caching;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class EksenPermissionBuilderCachingExtensions
{
    extension(IEksenPermissionBuilder builder)
    {
        public IEksenPermissionBuilder UseDistributedCache()
        {
            var services = builder.Services;
            services.AddScoped<IPermissionCache, DistributedPermissionCache>();
            return builder;
        }

        public IEksenPermissionBuilder UseInMemoryCache()
        {
            var services = builder.Services;
            services.AddScoped<IPermissionCache, InMemoryPermissionCache>();
            return builder;
        }
    }
}