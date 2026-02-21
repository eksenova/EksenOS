using Eksen.Permissions.AspNetCore;
using Microsoft.AspNetCore.Authorization;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddEksenPermissions(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        serviceCollection.AddTransient<IAuthorizationHandler, AppPermissionRequirementHandler>();

        serviceCollection.AddScoped<BindPermissionResultFilter>();

        serviceCollection.AddControllers(options =>
        {
            options.Filters.AddService<BindPermissionResultFilter>();
        });

        return serviceCollection;
    }
}