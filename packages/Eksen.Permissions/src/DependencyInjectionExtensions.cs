using Eksen.Identity.Roles;
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
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
        builder.AddValueObjects(valueObjectsBuilder =>
        {
            valueObjectsBuilder.Configure(options =>
            {
                options.AddAssembly(typeof(IPermissionChecker).Assembly);
            });
        });

        var services = builder.Services;

        services.TryAddScoped<IPermissionCache, DistributedPermissionCache>();
        services.TryAddScoped<IPermissionStore, PermissionStore<TUser, TRole, TTenant>>();
        services.TryAddScoped<IPermissionChecker, PermissionChecker<TUser, TTenant>>();

        if (configureAction != null)
        {
            var permissionBuilder = new EksenPermissionBuilder(builder);
            configureAction(permissionBuilder);
        }

        return builder;
    }
}

public interface IEksenPermissionBuilder
{
    IEksenBuilder EksenBuilder { get; }

    IEksenPermissionBuilder Configure(Action<EksenPermissionOptions> configureOptions);
}

public class EksenPermissionBuilder(IEksenBuilder eksenBuilder)
    : IEksenPermissionBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;

    public IEksenPermissionBuilder Configure(Action<EksenPermissionOptions> configureOptions)
    {
        this.Services.Configure(configureOptions);
        return this;
    }
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