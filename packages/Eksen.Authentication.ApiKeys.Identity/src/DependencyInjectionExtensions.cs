using Eksen.Authentication.ApiKeys;
using Eksen.Authentication.ApiKeys.Identity;
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    extension(IEksenApiKeyBuilder builder)
    {
        public IEksenApiKeyBuilder AddIdentitySupport<TUser, TTenant>()
            where TUser : class, IEksenUser<TTenant>
            where TTenant : class, IEksenTenant
        {
            var services = builder.Services;

            services.TryAddScoped<
                IApiKeyAuthenticator<EksenUserApiKey<TUser, TTenant>, EksenUserApiKeyId>,
                DefaultUserApiKeyAuthenticator<TUser, TTenant>>();

            return builder;
        }
    }
}
