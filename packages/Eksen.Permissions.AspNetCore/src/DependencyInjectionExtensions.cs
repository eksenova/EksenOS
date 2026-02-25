using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.Permissions.AspNetCore;
using Microsoft.AspNetCore.Authorization;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenPermissionBuilder<TUser, TRole, TTenant> AddAspNetCoreAuthorization<TUser, TRole, TTenant>(
        this IEksenPermissionBuilder<TUser, TRole, TTenant> builder)
        where TUser : class, IEksenUser<TTenant>
        where TRole : class, IEksenRole<TTenant>
        where TTenant : class, IEksenTenant
    {
        var services = builder.Services;

        services.AddTransient<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        services.AddTransient<IAuthorizationHandler, AppPermissionRequirementHandler>();

        services.AddScoped<BindPermissionResultFilter>();

        services.AddControllers(options =>
        {
            options.Filters.AddService<BindPermissionResultFilter>();
        });

        return builder;
    }
}