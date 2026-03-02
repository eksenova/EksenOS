#pragma warning disable IDE0130


using Eksen.Identity;
using Eksen.Identity.AspNetCore;
using Eksen.Identity.AspNetCore.Authentication;
using Eksen.Identity.AspNetCore.Security;
using Eksen.Identity.Roles;
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenIdentityBuilder AddAspNetCoreSupport<TUser, TRole, TTenant>(
        this IEksenIdentityBuilder builder
    )
        where TUser : class, IEksenUser<TTenant>
        where TRole : class, IEksenRole<TTenant>
        where TTenant : class, IEksenTenant
    {
        var services = builder.Services;

        services.AddHttpContextAccessor();

        services.TryAddScoped<EksenRoleManager<TRole, TTenant>>();
        services.TryAddTransient(typeof(RoleManager<TRole>),
            provider => provider.GetService(typeof(EksenRoleManager<TRole, TTenant>))!);

        services.TryAddScoped<EksenUserManager<TUser, TTenant>>();
        services.TryAddTransient(typeof(UserManager<TUser>),
            provider => provider.GetService(typeof(EksenUserManager<TUser, TTenant>))!);

        services.TryAddScoped<EksenUserSignInManager<TUser, TTenant>>();
        services.TryAddTransient(typeof(SignInManager<TUser>),
            provider => provider.GetService(typeof(EksenUserSignInManager<TUser, TTenant>))!);

        services.TryAddScoped<EksenUserStore<TUser, TRole, TTenant>>();
        services.TryAddTransient(typeof(IUserStore<TUser>),
            provider => provider.GetService(typeof(EksenUserStore<TUser, TRole, TTenant>))!);

        services.TryAddScoped<EksenRoleStore<TRole, TTenant>>();
        services.TryAddTransient(typeof(IRoleStore<TRole>),
            provider => provider.GetService(typeof(EksenRoleStore<TRole, TTenant>))!);

        services.TryAddScoped<EksenUserClaimsPrincipalFactory<TUser, TRole, TTenant>>();
        services.TryAddTransient(typeof(IUserClaimsPrincipalFactory<TUser>),
            provider => provider.GetService(typeof(EksenUserClaimsPrincipalFactory<TUser, TRole, TTenant>))!);
        services.TryAddTransient(typeof(UserClaimsPrincipalFactory<TUser, TRole>),
            provider => provider.GetService(typeof(EksenUserClaimsPrincipalFactory<TUser, TRole, TTenant>))!);

        services.TryAddScoped<IPasswordHasher<TUser>, EksenUserPasswordHasher<TUser, TTenant>>();
        services.TryAddScoped<ISecurityStampValidator, NullSecurityStampValidator>();

        services.TryAddScoped<IAuthContext, ClaimsBasedAuthContext>();

        services
            .AddIdentityCore<TUser>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<TRole>()
            .AddClaimsPrincipalFactory<EksenUserClaimsPrincipalFactory<TUser, TRole, TTenant>>();

        return builder;
    }
}