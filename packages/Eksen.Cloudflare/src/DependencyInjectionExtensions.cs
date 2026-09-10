using Eksen.Cloudflare;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddCloudflare(
        this IEksenBuilder builder,
        Action<IEksenCloudflareBuilder>? configureAction = null)
    {
        var services = builder.Services;

        services
            .AddOptions<EksenCloudflareOptions>()
            .BindConfiguration(EksenCloudflareOptions.DefaultConfigSectionPath)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient(CloudflareDefaults.HttpClientName);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ICloudflareIpRangeStore, InMemoryCloudflareIpRangeStore>();
        services.TryAddSingleton<ICloudflareIpRangeSource, HttpCloudflareIpRangeSource>();
        services.TryAddSingleton<ICloudflareIpRangeProvider, CloudflareIpRangeProvider>();

        services.AddHostedService<CloudflareIpRangeRefreshService>();

        if (configureAction != null)
        {
            var cloudflareBuilder = new EksenCloudflareBuilder(builder);
            configureAction(cloudflareBuilder);
        }

        return builder;
    }
}

public interface IEksenCloudflareBuilder
{
    IEksenBuilder EksenBuilder { get; }

    IEksenCloudflareBuilder Configure(Action<EksenCloudflareOptions> configureOptions);
}

public sealed class EksenCloudflareBuilder(IEksenBuilder eksenBuilder) : IEksenCloudflareBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;

    public IEksenCloudflareBuilder Configure(Action<EksenCloudflareOptions> configureOptions)
    {
        this.Services.Configure(configureOptions);
        return this;
    }
}

public static class EksenCloudflareBuilderExtensions
{
    extension(IEksenCloudflareBuilder builder)
    {
        public IServiceCollection Services
        {
            get { return builder.EksenBuilder.Services; }
        }
    }
}
