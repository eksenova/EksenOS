using Eksen.Permissions.AspNetCore;
using Microsoft.AspNetCore.Authorization;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenPermissionBuilder AddAspNetCoreSupport(
        this IEksenPermissionBuilder builder)
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