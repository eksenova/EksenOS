using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.Permissions;
using Eksen.Permissions.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddPermissions<TUser, TRole, TTenant>(this IEksenBuilder eksenBuilder)
        where TUser : class, IEksenUser<TTenant>
        where TRole : class, IEksenRole<TTenant>
        where TTenant : class, IEksenTenant
    {
        var services = eksenBuilder.Services;

        services.TryAddScoped<IPermissionCache, PermissionCache>();
        services.TryAddScoped<IPermissionStore, PermissionStore<TUser, TRole, TTenant>>();
        services.TryAddScoped<IPermissionChecker, PermissionChecker<TTenant>>();

        services.AddTransient<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        services.AddTransient<IAuthorizationHandler, AppPermissionRequirementHandler>();

        services.AddScoped<BindPermissionResultFilter>();

        services.AddControllers(options =>
        {
            options.Filters.AddService<BindPermissionResultFilter>();
        });

        return eksenBuilder;
    }
}