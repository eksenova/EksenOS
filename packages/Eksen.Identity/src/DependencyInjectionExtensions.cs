#pragma warning disable IDE0130


using Eksen.Identity;
using Eksen.Identity.Roles;
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddIdentity(
        this IEksenBuilder builder,
        Action<IEksenIdentityBuilder>? configureAction = null
    )
    {
        builder.AddValueObjects(valueObjectsBuilder =>
        {
            valueObjectsBuilder.Configure(options =>
            {
                options.AddAssembly(typeof(IAuthContext).Assembly);
            });
        });

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

        public IEksenIdentityBuilder AddUserRepository<TUserRepository, TUser, TTenant>()
            where TUserRepository : class, IEksenUserRepository<TUser, TTenant>
            where TUser : class, IEksenUser<TTenant>
            where TTenant : class, IEksenTenant
        {
            var services = builder.Services;
            services.AddScoped<IEksenUserRepository<TUser, TTenant>, TUserRepository>();
            return builder;
        }

        public IEksenIdentityBuilder AddRoleRepository<TRoleRepository, TRole, TTenant>()
            where TRoleRepository : class, IEksenRoleRepository<TRole, TTenant>
            where TRole : class, IEksenRole<TTenant>
            where TTenant : class, IEksenTenant
        {
            var services = builder.Services;
            services.AddScoped<IEksenRoleRepository<TRole, TTenant>, TRoleRepository>();
            return builder;
        }

        public IEksenIdentityBuilder AddTenantRepository<TTenantRepository, TTenant>()
            where TTenantRepository : class, IEksenTenantRepository<TTenant>
            where TTenant : class, IEksenTenant
        {
            var services = builder.Services;
            services.AddScoped<IEksenTenantRepository<TTenant>, TTenantRepository>();
            return builder;
        }
    }
}