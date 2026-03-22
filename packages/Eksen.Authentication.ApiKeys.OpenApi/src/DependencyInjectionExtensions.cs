using Eksen.Authentication.ApiKeys;
using Eksen.Authentication.ApiKeys.AspNetCore;
using Eksen.Authentication.ApiKeys.OpenApi;
using Eksen.ValueObjects.Entities;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    extension(IEksenApiKeyBuilder builder)
    {
        public IEksenApiKeyBuilder AddOpenApiSecurityScheme<TApiKey, TId>(
            EksenApiKeyAspNetCoreOptions<TApiKey, TId> options)
            where TApiKey : class, IEksenApiKey<TId>
            where TId : IEntityId<TId, System.Ulid>
        {
            var services = builder.Services;

            services.AddSingleton(options);
            services.AddOpenApi(openApiOptions =>
            {
                openApiOptions.AddDocumentTransformer<ApiKeySecuritySchemeTransformer<TApiKey, TId>>();
            });

            return builder;
        }
    }
}
