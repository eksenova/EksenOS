using Eksen.Telemetry;
using Eksen.Telemetry.AspNetCore.Enrichment;
using Eksen.Telemetry.AspNetCore.Health;
using Eksen.Telemetry.AspNetCore.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class EksenTelemetryAspNetCoreExtensions
{
    /// <summary>
    /// Registers the telemetry options (bound from <paramref name="telemetrySection"/>), the redactors and the
    /// request telemetry middlewares used by <see cref="UseEksenTelemetry"/>.
    /// </summary>
    public static IServiceCollection AddEksenTelemetryAspNetCore(
        this IServiceCollection services,
        IConfiguration telemetrySection,
        IHostEnvironment? environment = null)
    {
        services.AddEksenTelemetryCore(telemetrySection, environment);

        services.AddOptions<RequestBodyCaptureOptions>();
        services.AddOptions<InternalEndpointGuardOptions>();

        services.TryAddSingleton<HttpRequestEnrichmentCollector>();
        services.TryAddSingleton<TraceIdentifierMiddleware>();
        services.TryAddSingleton<RequestCompletionLoggingMiddleware>();
        services.TryAddSingleton<RequestTimingMiddleware>();
        services.TryAddSingleton<RequestBodyCaptureMiddleware>();
        services.TryAddSingleton<HttpEnrichmentMiddleware>();
        services.TryAddSingleton<HttpRouteEnrichmentMiddleware>();
        services.TryAddSingleton<RequestEnrichmentMiddleware>();
        services.TryAddSingleton<InternalEndpointGuardMiddleware>();

        return services;
    }

    /// <summary>
    /// Adds an <see cref="IRequestEnrichmentContributor"/>.
    /// </summary>
    public static IServiceCollection AddEksenRequestEnrichmentContributor<TContributor>(this IServiceCollection services)
        where TContributor : class, IRequestEnrichmentContributor
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRequestEnrichmentContributor, TContributor>());
        return services;
    }

    /// <summary>
    /// Never captures request bodies under these path prefixes.
    /// </summary>
    public static IServiceCollection AddEksenRequestBodyCaptureExclusions(
        this IServiceCollection services,
        params string[] pathPrefixes)
    {
        services.Configure<RequestBodyCaptureOptions>(options => options.ExcludedPrefixes.AddRange(pathPrefixes));
        return services;
    }

    /// <summary>
    /// Never captures request bodies of paths for which <paramref name="isExcluded"/> returns <see langword="true"/>,
    /// for routes a prefix cannot describe (a route whose first segment is a key, for example).
    /// </summary>
    public static IServiceCollection AddEksenRequestBodyCaptureExclusion(
        this IServiceCollection services,
        Func<string, bool> isExcluded)
    {
        ArgumentNullException.ThrowIfNull(isExcluded);

        services.Configure<RequestBodyCaptureOptions>(options => options.ExcludedPathPredicates.Add(isExcluded));
        return services;
    }

    /// <summary>
    /// Adds the health check services. Tag checks with <c>ready</c> to include them in <c>/healthz/ready</c>;
    /// <c>/healthz/live</c> runs only checks tagged <c>live</c>.
    /// </summary>
    public static IHealthChecksBuilder AddEksenHealthChecks(this IServiceCollection services)
    {
        return services.AddHealthChecks();
    }

    /// <summary>
    /// Adds the request telemetry middlewares: trace identifier, completion logging, timing, body capture and
    /// HTTP enrichment, in that order. Call it first in the pipeline.
    /// </summary>
    public static IApplicationBuilder UseEksenTelemetry(this IApplicationBuilder app)
    {
        return app
            .UseMiddleware<TraceIdentifierMiddleware>()
            .UseMiddleware<RequestCompletionLoggingMiddleware>()
            .UseMiddleware<RequestTimingMiddleware>()
            .UseMiddleware<RequestBodyCaptureMiddleware>()
            .UseMiddleware<HttpEnrichmentMiddleware>();
    }

    /// <summary>
    /// Adds the matched route to the log scope. Call it after <c>UseRouting</c>.
    /// </summary>
    public static IApplicationBuilder UseEksenRouteEnrichment(this IApplicationBuilder app)
    {
        return app.UseMiddleware<HttpRouteEnrichmentMiddleware>();
    }

    /// <summary>
    /// Runs the <see cref="IRequestEnrichmentContributor"/>s. Call it after <c>UseAuthentication</c>.
    /// </summary>
    public static IApplicationBuilder UseEksenRequestEnrichment(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestEnrichmentMiddleware>();
    }

    /// <summary>
    /// Sets the path prefixes guarded by <see cref="InternalEndpointGuardMiddleware"/> and the networks allowed
    /// to reach them (loopback only unless changed).
    /// </summary>
    public static IServiceCollection ConfigureEksenInternalEndpointGuard(
        this IServiceCollection services,
        Action<InternalEndpointGuardOptions> configure)
    {
        services.Configure(configure);
        return services;
    }

    /// <summary>
    /// Answers 404 on the guarded path prefixes to callers outside the allowed networks.
    /// </summary>
    public static IApplicationBuilder UseEksenInternalEndpointGuard(this IApplicationBuilder app)
    {
        return app.UseMiddleware<InternalEndpointGuardMiddleware>();
    }

    /// <summary>
    /// Maps <c>/healthz/live</c> (always 200 while the process runs, unless checks are tagged <c>live</c>) and
    /// <c>/healthz/ready</c> (checks tagged <c>ready</c>). With a management port both answer only when the connection
    /// arrived on that port, whatever the <c>Host</c> header says: a request to the public port with a spoofed
    /// <c>Host: x:port</c> gets 404, and a probe through a port mapping (whose <c>Host</c> names the mapped port) is
    /// answered. The response is the plain status, with no details.
    /// </summary>
    public static IEndpointRouteBuilder MapEksenHealthChecks(this IEndpointRouteBuilder endpoints, int? managementPort)
    {
        var live = endpoints.MapHealthChecks(EksenHealthChecks.LivePath, EksenHealthChecks.LiveOptions);
        var ready = endpoints.MapHealthChecks(EksenHealthChecks.ReadyPath, EksenHealthChecks.ReadyOptions);

        if (managementPort is { } port)
        {
            live.Add(builder => EksenHealthChecks.RequireLocalPort(builder, port));
            ready.Add(builder => EksenHealthChecks.RequireLocalPort(builder, port));
        }

        return endpoints;
    }

    /// <summary>
    /// Answers 404 to every request that arrived on <paramref name="managementPort"/> except the health endpoints,
    /// so the management listener added by <see cref="UseEksenManagementPort"/> does not also serve the
    /// application. Call it early in the pipeline, after <see cref="UseEksenTelemetry"/>.
    /// </summary>
    public static IApplicationBuilder UseEksenManagementPortIsolation(this IApplicationBuilder app, int managementPort)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(managementPort);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(managementPort, 65535);

        return app.Use(next => context =>
        {
            if (context.Connection.LocalPort == managementPort && !EksenHealthChecks.IsHealthPath(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }

            return next(context);
        });
    }

    /// <summary>
    /// Listens on <c>http://*:<paramref name="port"/></c> in addition to the configured endpoints
    /// (<c>ASPNETCORE_URLS</c>, or <c>ASPNETCORE_HTTP_PORTS</c>/<c>ASPNETCORE_HTTPS_PORTS</c>).
    /// </summary>
    public static IWebHostBuilder UseEksenManagementPort(this IWebHostBuilder builder, int port)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

        var urls = EksenHealthChecks.BuildListenUrls(
            builder.GetSetting(WebHostDefaults.ServerUrlsKey),
            builder.GetSetting(WebHostDefaults.HttpPortsKey),
            builder.GetSetting(WebHostDefaults.HttpsPortsKey),
            port);

        return builder.UseUrls(urls);
    }
}
