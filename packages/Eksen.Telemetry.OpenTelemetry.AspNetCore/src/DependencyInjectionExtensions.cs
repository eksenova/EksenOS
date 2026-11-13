using System.Diagnostics;
using Eksen.Telemetry;
using Eksen.Telemetry.Redaction;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Trace;

#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class EksenOpenTelemetryAspNetCoreExtensions
{
    /// <summary>
    /// Adds ASP.NET Core server spans: requests to <see cref="EksenTelemetryOptions.ExcludedPaths"/> are not
    /// traced, <c>url.path</c>, <c>url.full</c> and <c>http.route</c> pass through <see cref="IUrlPathScrubber"/>,
    /// <c>url.query</c> is redacted and 5xx responses get <c>error.type</c>. Has no effect unless
    /// <c>AddEksenTelemetry</c> enabled the OpenTelemetry SDK.
    /// </summary>
    public static IServiceCollection AddEksenAspNetCoreTelemetry(this IServiceCollection services)
    {
        services.AddEksenTelemetryPrimitives();

        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddAspNetCoreInstrumentation());

        services.AddOptions<AspNetCoreTraceInstrumentationOptions>()
            .Configure<IOptionsMonitor<EksenTelemetryOptions>, IUrlPathScrubber>((instrumentation, telemetryOptions, scrubber) =>
            {
                instrumentation.RecordException = telemetryOptions.CurrentValue.RecordExceptions;
                instrumentation.Filter = context => !telemetryOptions.CurrentValue.IsExcludedPath(context.Request.Path.Value);
                instrumentation.EnrichWithHttpRequest = (activity, request) => EnrichRequest(activity, request, scrubber);
                instrumentation.EnrichWithHttpResponse = (activity, response) => EnrichResponse(activity, response, scrubber);
            });

        return services;
    }

    internal static void EnrichRequest(Activity activity, HttpRequest request, IUrlPathScrubber scrubber)
    {
        try
        {
            SetScrubbedUrl(activity, request, scrubber);
        }
        catch (Exception)
        {
            // Telemetry must never fail a request.
        }
    }

    internal static void EnrichResponse(Activity activity, HttpResponse response, IUrlPathScrubber scrubber)
    {
        try
        {
            // The route is only known once routing ran, so it is scrubbed here rather than on the request.
            SetScrubbedUrl(activity, response.HttpContext.Request, scrubber);

            var route = activity.GetTagItem(EksenSemanticConventions.HttpRoute) as string
                        ?? (response.HttpContext.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
            if (!string.IsNullOrEmpty(route))
            {
                activity.SetTag(EksenSemanticConventions.HttpRoute, scrubber.Scrub(route));
            }

            if (response.StatusCode >= StatusCodes.Status500InternalServerError
                && activity.GetTagItem(EksenSemanticConventions.ErrorType) is null)
            {
                activity.SetTag(EksenSemanticConventions.ErrorType, response.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        catch (Exception)
        {
            // Telemetry must never fail a request.
        }
    }

    /// <summary>
    /// The longest <c>url.path</c>, <c>url.query</c> or <c>url.full</c> a server span keeps, the same limit the request
    /// log applies to client-supplied values. Longer values are cut (with <see cref="TelemetryValueRedactor.TruncationMarker"/>)
    /// before they are scrubbed, which also bounds the scrubbing work per request.
    /// </summary>
    public const int MaxUrlTagLength = 4096;

    private static void SetScrubbedUrl(Activity activity, HttpRequest request, IUrlPathScrubber scrubber)
    {
        if (request.Path.HasValue)
        {
            activity.SetTag(EksenSemanticConventions.UrlPath, scrubber.Scrub(Bounded(request.Path.Value!)));
        }

        if (request.QueryString.HasValue)
        {
            activity.SetTag(EksenSemanticConventions.UrlQuery, SensitiveUrlRedactor.Redact(Bounded(request.QueryString.Value!)));
        }

        if (activity.GetTagItem(EksenSemanticConventions.UrlFull) is not null)
        {
            activity.SetTag(EksenSemanticConventions.UrlFull, scrubber.ScrubUrl(Bounded(request.GetEncodedUrl())));
        }
    }

    private static string Bounded(string value)
    {
        return TelemetryValueRedactor.Truncate(value, MaxUrlTagLength);
    }
}
