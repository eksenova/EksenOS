using Eksen.Authentication.ApiKeys;
using Eksen.Authentication.ApiKeys.AspNetCore;
using Eksen.ValueObjects.Entities;
using Microsoft.AspNetCore.Authentication;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    extension(IEksenApiKeyBuilder builder)
    {
        public IEksenApiKeyBuilder AddAspNetCoreSupport<TApiKey, TId>(
            EksenApiKeyAspNetCoreOptions<TApiKey, TId> options)
            where TApiKey : class, IEksenApiKey<TId>
            where TId : IEntityId<TId, System.Ulid>
        {
            var services = builder.Services;

            services.AddAuthentication()
                .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler<TApiKey, TId>>(
                    options.Scheme,
                    schemeOptions =>
                    {
                        schemeOptions.AuthenticationMethod = options.AuthenticationMethod;
                    });

            return builder;
        }
    }
}
