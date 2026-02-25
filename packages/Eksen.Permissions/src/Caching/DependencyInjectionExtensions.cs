using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.Permissions;
using Eksen.Permissions.Caching;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class EksenPermissionBuilderCachingExtensions
{
    extension<TUser, TRole, TTenant>(IEksenPermissionBuilder<TUser, TRole, TTenant> builder)
        where TUser : class, IEksenUser<TTenant>
        where TRole : class, IEksenRole<TTenant>
        where TTenant : class, IEksenTenant
    {
        public IEksenPermissionBuilder<TUser, TRole, TTenant> UseDistributedCache()
        {
            var services = builder.Services;
            services.AddScoped<IPermissionCache, DistributedPermissionCache>();
            return builder;
        }

        public IEksenPermissionBuilder<TUser, TRole, TTenant> UseInMemoryCache()
        {
            var services = builder.Services;
            services.AddScoped<IPermissionCache, InMemoryPermissionCache>();
            return builder;
        }
    }
}