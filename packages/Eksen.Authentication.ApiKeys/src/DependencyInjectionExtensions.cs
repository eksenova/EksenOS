using Eksen.Authentication.ApiKeys;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddApiKeys(
        this IEksenBuilder builder,
        Action<IEksenApiKeyBuilder>? configureAction = null)
    {
        var services = builder.Services;

        services.TryAddSingleton<IApiKeyGenerator, GuidApiKeyGenerator>();

        if (configureAction != null)
        {
            var apiKeyBuilder = new EksenApiKeyBuilder(builder);
            configureAction(apiKeyBuilder);
        }

        return builder;
    }
}

public interface IEksenApiKeyBuilder
{
    IEksenBuilder EksenBuilder { get; }
}

public class EksenApiKeyBuilder(IEksenBuilder eksenBuilder) : IEksenApiKeyBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;
}

public static class EksenApiKeyBuilderExtensions
{
    extension(IEksenApiKeyBuilder builder)
    {
        public IServiceCollection Services
        {
            get { return builder.EksenBuilder.Services; }
        }
    }
}
