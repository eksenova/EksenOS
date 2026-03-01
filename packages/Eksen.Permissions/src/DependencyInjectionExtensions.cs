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
        Action<IEksenPermissionBuilder>? configureAction = null)
        where TUser : class, IEksenUser<TTenant>
        where TRole : class, IEksenRole<TTenant>
        where TTenant : class, IEksenTenant
    {
        var services = builder.Services;

        services.TryAddScoped<IPermissionCache, DistributedPermissionCache>();
        services.TryAddScoped<IPermissionStore, PermissionStore<TUser, TRole, TTenant>>();
        services.TryAddScoped<IPermissionChecker, PermissionChecker<TUser, TTenant>>();

        services.Configure<EksenPermissionOptions>(options =>
        {
            if (configureAction != null)
            {
                var permissionBuilder = new EksenPermissionBuilder(builder, options);
                configureAction(permissionBuilder);
            }
        });

        return builder;
    }
}

public interface IEksenPermissionBuilder
{
    IEksenBuilder EksenBuilder { get; }

    EksenPermissionOptions Options { get; }
}

public class EksenPermissionBuilder(IEksenBuilder eksenBuilder, EksenPermissionOptions options)
    : IEksenPermissionBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;

    public EksenPermissionOptions Options { get; } = options;
}

public static class EksenPermissionBuilderExtensions
{
    extension(IEksenPermissionBuilder builder)
    {
        public IServiceCollection Services
        {
            get { return builder.EksenBuilder.Services; }
        }
    }
}