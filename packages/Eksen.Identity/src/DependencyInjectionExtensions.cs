#pragma warning disable IDE0130


using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.Identity;
using Eksen.Identity.Abstractions;
using Eksen.Identity.Authentication;
using Eksen.Identity.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddIdentity<TUser, TRole, TTenant>(
        this IEksenBuilder eksenBuilder
    )
        where TUser : class, IEksenUser<TTenant>
        where TRole : class, IEksenRole<TTenant>
        where TTenant : class, IEksenTenant
    {
        var services = eksenBuilder.Services;

        services.AddHttpContextAccessor();

        services.TryAddScoped<EksenUserManager<TUser, TTenant>>();
        services.TryAddScoped<EksenRoleManager<TRole, TTenant>>();

        services.TryAddScoped<EksenUserSignInManager<TUser, TTenant>>();

        services.TryAddScoped(typeof(RoleManager<TUser>),
            provider => provider.GetService(typeof(EksenRoleManager<TRole, TTenant>))!);

        services.TryAddScoped(typeof(UserManager<TUser>),
            provider => provider.GetService(typeof(EksenUserManager<TUser, TTenant>))!);

        services.TryAddScoped(typeof(SignInManager<TUser>),
            provider => provider.GetService(typeof(EksenUserSignInManager<TUser, TTenant>))!);

        services.TryAddScoped<EksenUserStore<TUser, TTenant>>();
        services.TryAddScoped(typeof(IUserStore<TUser>),
            provider => provider.GetService(typeof(EksenUserStore<TUser, TTenant>))!);

        services.TryAddScoped<EksenRoleStore<TRole, TTenant>>();
        services.TryAddScoped(typeof(IRoleStore<TRole>),
            provider => provider.GetService(typeof(EksenRoleStore<TRole, TTenant>))!);

        services.TryAddScoped<EksenUserClaimsPrincipalFactory<TUser, TRole, TTenant>>();
        services.TryAddScoped(typeof(IUserClaimsPrincipalFactory<TUser>),
            provider => provider.GetService(typeof(EksenUserClaimsPrincipalFactory<TUser, TRole, TTenant>))!);
        services.TryAddScoped(typeof(UserClaimsPrincipalFactory<TUser, TRole>),
            provider => provider.GetService(typeof(EksenUserClaimsPrincipalFactory<TUser, TRole, TTenant>))!);

        services.TryAddScoped<IPasswordHasher<TUser>, EksenUserPasswordHasher<TUser, TTenant>>();
        services.TryAddScoped<ISecurityStampValidator, NullSecurityStampValidator>();

        services.TryAddScoped<IAuthContext, AuthContext>();

        services
            .AddIdentityCore<TUser>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<TRole>()
            .AddClaimsPrincipalFactory<EksenUserClaimsPrincipalFactory<TUser, TRole, TTenant>>();

        return eksenBuilder;
    }
}