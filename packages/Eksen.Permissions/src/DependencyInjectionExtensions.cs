using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.Permissions;
using Eksen.Permissions.Caching;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddPermissions<TUser, TRole, TTenant>(
        this IEksenBuilder builder,
        Action<IEksenPermissionBuilder<TUser, TRole, TTenant>>? configureAction = null)
        where TUser : class, IEksenUser<TTenant>
        where TRole : class, IEksenRole<TTenant>
        where TTenant : class, IEksenTenant
    {
        var services = builder.Services;

        services.TryAddScoped<IPermissionCache, DistributedPermissionCache>();
        services.TryAddScoped<IPermissionStore, PermissionStore<TUser, TRole, TTenant>>();
        services.TryAddScoped<IPermissionChecker, PermissionChecker<TUser, TTenant>>();

        if (configureAction != null)
        {
            var permissionBuilder = new EksenPermissionBuilder<TUser, TRole, TTenant>(builder);
            configureAction(permissionBuilder);
        }

        return builder;
    }
}

public interface IEksenPermissionBuilder<TUser, TRole, TTenant>
    where TUser : class, IEksenUser<TTenant>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    IEksenBuilder EksenBuilder { get; }
}

public class EksenPermissionBuilder<TUser, TRole, TTenant>(IEksenBuilder eksenBuilder)
    : IEksenPermissionBuilder<TUser, TRole, TTenant>
    where TUser : class, IEksenUser<TTenant>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;
}

public static class EksenPermissionBuilderExtensions
{
    extension<TUser, TRole, TTenant>(IEksenPermissionBuilder<TUser, TRole, TTenant> builder)
        where TUser : class, IEksenUser<TTenant>
        where TRole : class, IEksenRole<TTenant>
        where TTenant : class, IEksenTenant
    {
        public IServiceCollection Services
        {
            get { return builder.EksenBuilder.Services; }
        }
    }
}