using System.Text.RegularExpressions;
using Eksen.Telemetry;
using Eksen.Telemetry.Background;
using Eksen.Telemetry.Outbound;
using Eksen.Telemetry.Redaction;
using Eksen.Telemetry.Registry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class EksenTelemetryServiceCollectionExtensions
{
    private static readonly TimeSpan ScrubRuleTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Registers <see cref="EksenTelemetryOptions"/> (bound from <paramref name="telemetrySection"/>, with
    /// <c>OTEL_*</c> fallbacks) and the telemetry primitives: redactors, the path scrubber, the outbound and
    /// background job metrics and the source/meter registry. Registers no OpenTelemetry SDK component.
    /// </summary>
    public static IServiceCollection AddEksenTelemetryCore(
        this IServiceCollection services,
        IConfiguration telemetrySection,
        IHostEnvironment? environment = null,
        Action<EksenTelemetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(telemetrySection);

        // Binding runs once even when several packages call this method with the same section.
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(EksenTelemetryOptionsBindingMarker)))
        {
            services.AddSingleton(new EksenTelemetryOptionsBindingMarker());
            services.AddOptions<EksenTelemetryOptions>()
                .Configure(options => EksenTelemetryOptionsResolver.Apply(options, telemetrySection, environment));
        }

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services.AddEksenTelemetryPrimitives();
    }

    /// <summary>
    /// Registers the telemetry primitives with whatever <see cref="EksenTelemetryOptions"/> configuration is
    /// already present. Safe to call more than once.
    /// </summary>
    public static IServiceCollection AddEksenTelemetryPrimitives(this IServiceCollection services)
    {
        services.AddOptions<EksenTelemetryOptions>();
        services.AddOptions<OutboundBodyCaptureOptions>();
        services.AddMetrics();

        services.TryAddSingleton(serviceProvider =>
            new HeaderRedactor(serviceProvider.GetRequiredService<IOptions<EksenTelemetryOptions>>().Value.RedactedHeaders));
        services.TryAddSingleton<IUrlPathScrubber, DefaultUrlPathScrubber>();
        services.TryAddSingleton<IOutboundBodyRedactor, NullOutboundBodyRedactor>();
        services.TryAddSingleton<OutboundHttpMetrics>();
        services.TryAddSingleton<BackgroundJobTelemetry>();

        services.AddEksenTelemetrySource(EksenTelemetry.HttpOutbound);
        services.AddEksenTelemetryMeter(EksenTelemetry.HttpOutbound);
        services.AddEksenTelemetrySource(EksenTelemetry.Background);
        services.AddEksenTelemetryMeter(EksenTelemetry.Background);

        return services;
    }

    /// <summary>
    /// Declares an <see cref="System.Diagnostics.ActivitySource"/> the OpenTelemetry wiring must listen to.
    /// Registering the same name twice has no effect.
    /// </summary>
    public static IServiceCollection AddEksenTelemetrySource(this IServiceCollection services, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(EksenTelemetrySourceRegistration)
                                        && descriptor.ImplementationInstance is EksenTelemetrySourceRegistration existing
                                        && existing.Name == name))
        {
            services.AddSingleton(new EksenTelemetrySourceRegistration(name));
        }

        return services;
    }

    /// <summary>
    /// Declares a <see cref="System.Diagnostics.Metrics.Meter"/> the OpenTelemetry wiring must collect.
    /// Registering the same name twice has no effect.
    /// </summary>
    public static IServiceCollection AddEksenTelemetryMeter(this IServiceCollection services, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(EksenTelemetryMeterRegistration)
                                        && descriptor.ImplementationInstance is EksenTelemetryMeterRegistration existing
                                        && existing.Name == name))
        {
            services.AddSingleton(new EksenTelemetryMeterRegistration(name));
        }

        return services;
    }

    /// <summary>
    /// Adds a path rewrite applied by <see cref="IUrlPathScrubber"/>, for example
    /// <c>^/[0-9a-fA-F-]{36}/(LicenseChecker)/</c> to <c>/{license}/$1/</c>. Rules run in registration order.
    /// </summary>
    public static IServiceCollection AddEksenUrlPathScrubRule(
        this IServiceCollection services,
        string pattern,
        string replacement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentNullException.ThrowIfNull(replacement);

        var regex = new Regex(
            pattern,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            ScrubRuleTimeout);

        services.AddSingleton(new UrlPathScrubRule(regex, replacement));
        return services;
    }

    /// <summary>
    /// Adds <see cref="OutboundLoggingHandler"/> to every <see cref="IHttpClientFactory"/> client. The client name
    /// becomes the default <c>eksen.outbound.target</c>.
    /// </summary>
    public static IServiceCollection AddEksenOutboundTelemetry(this IServiceCollection services)
    {
        services.AddEksenTelemetryPrimitives();

        // A handler-builder filter (rather than ConfigureHttpClientDefaults, whose builder has no name) gives every
        // handler the name of the client it belongs to.
        services.AddHttpClient();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, OutboundTelemetryHandlerBuilderFilter>());

        return services;
    }

    /// <summary>
    /// Opts the named HTTP clients in to body capture (still gated by
    /// <see cref="EksenTelemetryOptions.CaptureOutboundBodies"/>).
    /// </summary>
    public static IServiceCollection AddEksenOutboundBodyCapture(
        this IServiceCollection services,
        params string[] clientNames)
    {
        services.Configure<OutboundBodyCaptureOptions>(options =>
        {
            foreach (var clientName in clientNames)
            {
                if (!string.IsNullOrWhiteSpace(clientName))
                {
                    options.Clients.Add(clientName);
                }
            }
        });

        return services;
    }
}

internal sealed class EksenTelemetryOptionsBindingMarker;
