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
        this IEksenBuilder builder,
        Action<IEksenIdentityBuilder>? configureAction = null
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

        services.TryAddScoped<EksenUserStore<TUser, TTenant>>();
        services.TryAddTransient(typeof(IUserStore<TUser>),
            provider => provider.GetService(typeof(EksenUserStore<TUser, TTenant>))!);

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

        services.TryAddScoped<IAuthContext, AuthContext>();

        services
            .AddIdentityCore<TUser>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<TRole>()
            .AddClaimsPrincipalFactory<EksenUserClaimsPrincipalFactory<TUser, TRole, TTenant>>();

        if (configureAction != null)
        {
            var identityBuilder = new EksenIdentityBuilder(builder);
            configureAction(identityBuilder);
        }

        return builder;
    }
}

public class EksenIdentityBuilder(IEksenBuilder eksenBuilder) : IEksenIdentityBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;
}

// ReSharper disable UnusedTypeParameter
public interface IEksenIdentityBuilder
{
    IEksenBuilder EksenBuilder { get; }
}

// ReSharper restore UnusedTypeParameter

public static class EksenIdentityBuilderExtensions
{
    extension(IEksenIdentityBuilder builder)
    {
        public IServiceCollection Services
        {
            get { return builder.EksenBuilder.Services; }
        }

        public IEksenIdentityBuilder AddUserRepository<TUser, TTenant, TUserRepository>()
            where TUserRepository : class, IEksenUserRepository<TUser, TTenant>
            where TUser : class, IEksenUser<TTenant>
            where TTenant : class, IEksenTenant
        {
            var services = builder.Services;
            services.AddScoped<IEksenUserRepository<TUser, TTenant>, TUserRepository>();
            return builder;
        }

        public IEksenIdentityBuilder AddRoleRepository<TRole, TTenant, TRoleRepository>()
            where TRoleRepository : class, IEksenRoleRepository<TRole, TTenant>
            where TRole : class, IEksenRole<TTenant>
            where TTenant : class, IEksenTenant
        {
            var services = builder.Services;
            services.AddScoped<IEksenRoleRepository<TRole, TTenant>, TRoleRepository>();
            return builder;
        }

        public IEksenIdentityBuilder AddTenantRepository<TTenant, TTenantRepository>()
            where TTenantRepository : class, IEksenTenantRepository<TTenant>
            where TTenant : class, IEksenTenant
        {
            var services = builder.Services;
            services.AddScoped<IEksenTenantRepository<TTenant>, TTenantRepository>();
            return builder;
        }
    }
}