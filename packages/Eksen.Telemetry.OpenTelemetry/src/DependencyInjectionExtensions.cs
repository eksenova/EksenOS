using Eksen.Telemetry;
using Eksen.Telemetry.OpenTelemetry;
using Eksen.Telemetry.OpenTelemetry.Logging;
using Eksen.Telemetry.Outbound;
using Eksen.Telemetry.Redaction;
using Eksen.Telemetry.Registry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Instrumentation.Http;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class EksenOpenTelemetryExtensions
{
    public const string ApplicationSourceWildcard = "ImperialPlugins.*";

    public static readonly IReadOnlyList<double> DurationBuckets =
    [
        0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10, 30, 60
    ];

    public static readonly IReadOnlyList<string> DurationHistograms =
    [
        "http.server.request.duration",
        "http.client.request.duration",
        "eksen.outbound.http.request.duration",
        "messaging.process.duration",
        "eksen.db.command.duration",
        "eksen.background.job.duration"
    ];

    public static readonly IReadOnlyList<string> FrameworkMeters =
    [
        "Microsoft.AspNetCore.Hosting",
        "Microsoft.AspNetCore.Server.Kestrel",
        "Microsoft.AspNetCore.Routing",
        "Microsoft.AspNetCore.Diagnostics",
        "System.Net.Http",
        "System.Net.NameResolution",
        "Npgsql"
    ];

    /// <summary>
    /// Wires telemetry for a host. Options come from <paramref name="telemetrySection"/> (with <c>OTEL_*</c>
    /// fallbacks). When telemetry is disabled or has no endpoint, only the options, the source/meter registry and
    /// the <c>Eksen.Telemetry</c> primitives are registered; otherwise the OpenTelemetry SDK exports traces,
    /// metrics and logs over OTLP.
    /// </summary>
    public static IServiceCollection AddEksenTelemetry(
        this IServiceCollection services,
        IConfiguration telemetrySection,
        IHostEnvironment env,
        Action<EksenTelemetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(telemetrySection);
        ArgumentNullException.ThrowIfNull(env);

        services.AddEksenTelemetryCore(telemetrySection, env, configure);

        var options = EksenTelemetryOptionsResolver.Resolve(telemetrySection, env);
        configure?.Invoke(options);

        if (!options.IsExportEnabled)
        {
            return services;
        }

        foreach (var name in EksenTelemetry.AllNames)
        {
            services.AddEksenTelemetrySource(name);
            services.AddEksenTelemetryMeter(name);
        }

        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

        services.AddSingleton<EksenOpenTelemetryMarker>();

        services.AddOpenTelemetry()
            .ConfigureResource(resource => ConfigureResource(resource, options, env))
            .WithTracing(tracing => ConfigureTracing(services, tracing, options))
            .WithMetrics(metrics => ConfigureMetrics(metrics, options))
            .WithLogging(
                logging => ConfigureLogging(logging, options),
                loggerOptions =>
                {
                    loggerOptions.IncludeScopes = true;
                    loggerOptions.IncludeFormattedMessage = true;
                    loggerOptions.ParseStateValues = true;
                });

        // Sources and meters registered by packages are read from the container, so registration order
        // between AddEksenTelemetry and the packages does not matter.
        services.ConfigureOpenTelemetryTracerProvider((serviceProvider, tracing) =>
        {
            foreach (var name in EksenTelemetryRegistry.GetSourceNames(serviceProvider))
            {
                tracing.AddSource(name);
            }
        });

        services.ConfigureOpenTelemetryMeterProvider((serviceProvider, metrics) =>
        {
            foreach (var name in EksenTelemetryRegistry.GetMeterNames(serviceProvider))
            {
                metrics.AddMeter(name);
            }
        });

        // Processors cannot rewrite scopes, so the exported scopes are redacted by decorating the provider.
        ScopeRedactingLoggerProvider.Decorate(services);

        ConfigureConsole(services, options);

        return services;
    }

    /// <summary>
    /// <see cref="AddEksenTelemetry(IServiceCollection, IConfiguration, IHostEnvironment, Action{EksenTelemetryOptions}?)"/>
    /// for <c>AddEksen(...)</c> callbacks.
    /// </summary>
    public static IEksenBuilder AddTelemetry(
        this IEksenBuilder builder,
        IConfiguration telemetrySection,
        IHostEnvironment env,
        Action<EksenTelemetryOptions>? configure = null)
    {
        builder.Services.AddEksenTelemetry(telemetrySection, env, configure);
        return builder;
    }

    /// <summary>
    /// True when <see cref="AddEksenTelemetry"/> registered the OpenTelemetry SDK.
    /// </summary>
    public static bool IsEksenOpenTelemetryEnabled(this IServiceCollection services)
    {
        return services.Any(descriptor => descriptor.ServiceType == typeof(EksenOpenTelemetryMarker));
    }

    private static void ConfigureResource(ResourceBuilder resource, EksenTelemetryOptions options, IHostEnvironment env)
    {
        resource
            .AddService(
                serviceName: options.ServiceName ?? env.ApplicationName,
                serviceNamespace: options.ServiceNamespace,
                serviceVersion: EksenTelemetryResource.ResolveServiceVersion(),
                autoGenerateServiceInstanceId: false,
                serviceInstanceId: EksenTelemetryResource.ServiceInstanceId)
            .AddAttributes(EksenTelemetryResource.BuildAttributes(options, env))
            .AddContainerDetector()
            // Last, so OTEL_RESOURCE_ATTRIBUTES (k8s.* from the downward API, or an explicit
            // deployment.environment) wins over the defaults above.
            .AddEnvironmentVariableDetector();
    }

    private static void ConfigureTracing(IServiceCollection services, TracerProviderBuilder tracing, EksenTelemetryOptions options)
    {
        tracing
            .SetSampler(EksenSampler.Create(options.SamplingRatio, options.TraceOrphanClientSpans))
            .AddSource(EksenTelemetry.SourceWildcard)
            .AddSource(ApplicationSourceWildcard)
            .AddSource("Azure.*");

        foreach (var source in options.AdditionalSources)
        {
            if (!string.IsNullOrWhiteSpace(source))
            {
                tracing.AddSource(source);
            }
        }

        tracing.AddHttpClientInstrumentation();
        services.AddOptions<HttpClientTraceInstrumentationOptions>()
            .Configure<IUrlPathScrubber>((http, scrubber) =>
            {
                http.RecordException = options.RecordExceptions;
                http.EnrichWithHttpRequestMessage = (activity, request) => EnrichOutboundRequest(activity, request, scrubber);
            });

        tracing.AddNpgsql();
        tracing.AddEntityFrameworkCoreInstrumentation();

        // Resolves an IConnectionMultiplexer from the container when the provider is built; without one it
        // instruments nothing.
        tracing.AddRedisInstrumentation();

        if (!options.CaptureDbStatements)
        {
            tracing.AddProcessor(new DbStatementFilterProcessor());
        }

        tracing.AddOtlpExporter(exporter => ConfigureExporter(exporter, options, "traces"));
    }

    private static void ConfigureMetrics(MeterProviderBuilder metrics, EksenTelemetryOptions options)
    {
        metrics
            .AddMeter(EksenTelemetry.SourceWildcard)
            .AddMeter(ApplicationSourceWildcard);

        foreach (var meter in FrameworkMeters)
        {
            metrics.AddMeter(meter);
        }

        foreach (var meter in options.AdditionalMeters)
        {
            if (!string.IsNullOrWhiteSpace(meter))
            {
                metrics.AddMeter(meter);
            }
        }

        metrics
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation();

        foreach (var instrument in DurationHistograms)
        {
            metrics.AddView(instrument, new ExplicitBucketHistogramConfiguration { Boundaries = DurationBuckets.ToArray() });
        }

        metrics
            .SetExemplarFilter(ExemplarFilterType.TraceBased)
            .AddOtlpExporter((exporter, reader) =>
            {
                ConfigureExporter(exporter, options, "metrics");
                reader.TemporalityPreference = MetricReaderTemporalityPreference.Cumulative;
            });
    }

    private static void ConfigureLogging(LoggerProviderBuilder logging, EksenTelemetryOptions options)
    {
        logging
            .AddProcessor(new RedactingLogProcessor())
            .AddProcessor(new ExceptionNormalizingLogProcessor())
            .AddOtlpExporter(exporter => ConfigureExporter(exporter, options, "logs"));
    }

    private static void ConfigureConsole(IServiceCollection services, EksenTelemetryOptions options)
    {
        services.AddLogging(logging =>
        {
            if (options.ConsoleJson == true)
            {
                // The JSON console format with the same redaction as the OTLP log pipeline.
                logging.AddConsole(console => console.FormatterName = RedactingJsonConsoleFormatter.FormatterName);
                logging.AddConsoleFormatter<RedactingJsonConsoleFormatter, JsonConsoleFormatterOptions>(console =>
                {
                    console.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
                    console.UseUtcTimestamp = true;
                    console.IncludeScopes = true;
                });

                var minimumLevel = options.ConsoleMinimumLevel;
                logging.AddFilter<ConsoleLoggerProvider>(level => level >= minimumLevel);
            }
            else
            {
                logging.AddSimpleConsole();
            }
        });
    }

    private static void ConfigureExporter(OtlpExporterOptions exporter, EksenTelemetryOptions options, string signal)
    {
        var endpoint = options.OtlpEndpoint!.Trim();

        if (options.IsHttpProtobuf)
        {
            exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
            exporter.Endpoint = new Uri($"{endpoint.TrimEnd('/')}/v1/{signal}");
        }
        else
        {
            exporter.Protocol = OtlpExportProtocol.Grpc;
            exporter.Endpoint = new Uri(endpoint);
        }

        if (!string.IsNullOrWhiteSpace(options.OtlpHeaders))
        {
            exporter.Headers = options.OtlpHeaders;
        }
    }

    private static void EnrichOutboundRequest(System.Diagnostics.Activity activity, HttpRequestMessage request, IUrlPathScrubber scrubber)
    {
        try
        {
            if (request.RequestUri is { } uri && activity.GetTagItem(EksenSemanticConventions.UrlFull) is not null)
            {
                activity.SetTag(EksenSemanticConventions.UrlFull, scrubber.ScrubUrl(uri));
            }

            if (request.Options.TryGetValue(OutboundRequestOptions.Target, out var target) && !string.IsNullOrWhiteSpace(target))
            {
                activity.SetTag(EksenSemanticConventions.EksenOutboundTarget, target);
            }
        }
        catch (Exception)
        {
            // Telemetry must never fail a request.
        }
    }
}

internal sealed class EksenOpenTelemetryMarker;
