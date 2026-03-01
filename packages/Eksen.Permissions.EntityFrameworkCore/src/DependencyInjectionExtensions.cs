using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.EntityFrameworkCore;
using Eksen.Permissions;
using Eksen.Permissions.EntityFrameworkCore;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    extension(IEksenPermissionBuilder builder)
    {
        public IEksenPermissionBuilder UseEntityFrameworkCore<TUser, TRole, TTenant, TDbContext>()
            where TDbContext : EksenDbContext
            where TUser : class, IEksenUser<TTenant>
            where TRole : class, IEksenRole<TTenant>
            where TTenant : class, IEksenTenant
        {
            var services = builder.Services;

            services.AddScoped<IEksenPermissionDefinitionRepository, EfCoreEksenPermissionDefinitionRepository<TDbContext>>();
            services
                .AddScoped<IEksenUserPermissionRepository<TUser, TTenant>,
                    EfCoreEksenUserPermissionRepository<TDbContext, TUser, TTenant>>();
            services
                .AddScoped<IEksenRolePermissionRepository<TRole, TTenant>,
                    EfCoreEksenRolePermissionRepository<TDbContext, TRole, TTenant>>();
            services
                .AddScoped<IEksenUserRoleRepository<TUser, TRole, TTenant>,
                    EfCoreEksenUserRoleRepository<TDbContext, TUser, TRole, TTenant>>();

            return builder;
        }
    }
}