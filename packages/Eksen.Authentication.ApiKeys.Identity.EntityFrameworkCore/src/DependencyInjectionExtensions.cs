using Eksen.Authentication.ApiKeys;
using Eksen.Authentication.ApiKeys.Identity;
using Eksen.Authentication.ApiKeys.Identity.EntityFrameworkCore;
using Eksen.EntityFrameworkCore;
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    extension(IEksenApiKeyBuilder builder)
    {
        public IEksenApiKeyBuilder UseEntityFrameworkCore<TUser, TTenant, TDbContext>()
            where TDbContext : EksenDbContext
            where TUser : class, IEksenUser<TTenant>
            where TTenant : class, IEksenTenant
        {
            var services = builder.Services;

            services.AddScoped<IEksenUserApiKeyRepository<TUser, TTenant>,
                EfCoreEksenUserApiKeyRepository<TDbContext, TUser, TTenant>>();

            return builder;
        }
    }
}
