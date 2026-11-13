using System.Diagnostics;
using Eksen.Telemetry.AspNetCore.Middlewares;
using Eksen.Telemetry.Redaction;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;

namespace Eksen.Telemetry.AspNetCore.Enrichment;

/// <summary>
/// Collects the HTTP request, response and route fields written to logs. Paths pass through
/// <see cref="IUrlPathScrubber"/>, query strings through <see cref="SensitiveUrlRedactor"/>, headers through
/// <see cref="HeaderRedactor"/> and captured bodies through <see cref="SensitiveBodyRedactor"/>. Header, query and
/// route values are cut to <see cref="MaxValueLength"/> characters, so an oversized value sent by a client does not
/// travel through every log record of its request.
/// </summary>
public sealed class HttpRequestEnrichmentCollector(
    HeaderRedactor headerRedactor,
    IUrlPathScrubber urlPathScrubber
)
{
    public const int MaxValueLength = 4096;

    private static readonly HashSet<string> FrameworkRouteValueKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "controller", "action", "area", "page", "handler"
    };

    public IReadOnlyList<RequestEnrichmentProperty> CollectHttpRequest(HttpContext context)
    {
        return CollectHttpRequest(context, includeBody: true);
    }

    /// <summary>
    /// Collects the request fields. Pass <paramref name="includeBody"/> <see langword="false"/> for the log scope,
    /// which every record of the request repeats; the captured body belongs on the completion event only.
    /// </summary>
    public IReadOnlyList<RequestEnrichmentProperty> CollectHttpRequest(HttpContext context, bool includeBody)
    {
        var request = context.Request;
        var activity = Activity.Current;

        var path = request.Path.HasValue ? urlPathScrubber.Scrub(request.Path.Value!) : null;
        var query = request.QueryString.HasValue ? SensitiveUrlRedactor.Redact(request.QueryString.Value!) : null;
        var capturedBody = includeBody ? context.Items[RequestBodyCaptureMiddleware.HttpContextItemKey] as string : null;

        var properties = new List<RequestEnrichmentProperty>
        {
            new("eksen.request_body", capturedBody is null ? null : SensitiveBodyRedactor.Redact(capturedBody, request.ContentType), ActivityTag: false),
            new("eksen.http.request_id", context.TraceIdentifier, ActivityTag: false),
            new("eksen.http.connection_id", context.Connection.Id, ActivityTag: false),
            new(EksenSemanticConventions.HttpRequestMethod, request.Method, ActivityTag: false),
            new(EksenSemanticConventions.UrlPath, path, ActivityTag: false),
            new(EksenSemanticConventions.UrlFull, urlPathScrubber.ScrubUrl(request.GetEncodedUrl()), ActivityTag: false),
            new("url.scheme", request.Scheme.NullIfEmpty(), ActivityTag: false),
            new(EksenSemanticConventions.UrlQuery, query, ActivityTag: false),
            new(EksenSemanticConventions.ServerAddress, request.Host.Host.NullIfEmpty(), ActivityTag: false),
            new(EksenSemanticConventions.ServerPort, request.Host.Port, ActivityTag: false),
            new("network.protocol.version", ResolveProtocolVersion(request.Protocol), ActivityTag: false),
            new("client.address", Bound(request.Headers["X-Forwarded-For"].ToString().NullIfEmpty()), ActivityTag: false),
            new("user_agent.original", Bound(request.Headers.UserAgent.ToString().NullIfEmpty()), ActivityTag: false),
            new("http.request.body.size", request.ContentLength, ActivityTag: false),
            new("eksen.trace_id", activity?.TraceId.ToHexString(), ActivityTag: false),
            new("eksen.span_id", activity?.SpanId.ToHexString(), ActivityTag: false)
        };

        foreach (var (key, value) in request.Query)
        {
            properties.Add(new RequestEnrichmentProperty(
                $"eksen.http.query.{key}",
                SensitiveFieldName.IsSensitive(key) ? SensitiveUrlRedactor.RedactedValue : Bound(value.ToString()),
                ActivityTag: false));
        }

        AddHeaderProperties(properties, "http.request.header.", request.Headers);

        return properties;
    }

    public IReadOnlyList<RequestEnrichmentProperty> CollectHttpResponse(HttpContext context)
    {
        var response = context.Response;

        var properties = new List<RequestEnrichmentProperty>
        {
            new(EksenSemanticConventions.HttpResponseStatusCode, response.StatusCode, ActivityTag: false),
            new("http.response.body.size", response.ContentLength, ActivityTag: false),
            new("eksen.http.elapsed_ms", GetElapsedMilliseconds(context), ActivityTag: false)
        };

        AddHeaderProperties(properties, "http.response.header.", response.Headers);

        return properties;
    }

    public IReadOnlyList<RequestEnrichmentProperty> CollectHttpRoute(HttpContext context)
    {
        var request = context.Request;
        var endpoint = context.GetEndpoint();
        var routePattern = (endpoint as RouteEndpoint)?.RoutePattern.RawText;

        var properties = new List<RequestEnrichmentProperty>
        {
            new(EksenSemanticConventions.HttpRoute, routePattern is null ? null : urlPathScrubber.Scrub(routePattern), ActivityTag: false),
            new("eksen.http.route.controller", request.RouteValues["controller"]?.ToString(), ActivityTag: false),
            new("eksen.http.route.action", request.RouteValues["action"]?.ToString(), ActivityTag: false),
            new("eksen.http.route.endpoint_name", endpoint?.DisplayName, ActivityTag: false)
        };

        foreach (var (key, value) in request.RouteValues)
        {
            if (FrameworkRouteValueKeys.Contains(key))
            {
                continue;
            }

            properties.Add(new RequestEnrichmentProperty(
                $"eksen.http.route.parameters.{key}",
                SensitiveFieldName.IsSensitive(key) ? SensitiveUrlRedactor.RedactedValue : Bound(value?.ToString()),
                ActivityTag: false));
        }

        return properties;
    }

    private void AddHeaderProperties(List<RequestEnrichmentProperty> properties, string prefix, IHeaderDictionary headers)
    {
        foreach (var (key, value) in headers)
        {
            var headerValue = value.ToString().NullIfEmpty();
            if (headerValue is null)
            {
                continue;
            }

            properties.Add(new RequestEnrichmentProperty(
                prefix + key.ToLowerInvariant(),
                Bound(headerRedactor.Redact(key, headerValue)),
                ActivityTag: false));
        }
    }

    private static string? Bound(string? value)
    {
        return value is null ? null : TelemetryValueRedactor.Truncate(value, MaxValueLength);
    }

    private static string? ResolveProtocolVersion(string? protocol)
    {
        if (string.IsNullOrEmpty(protocol))
        {
            return null;
        }

        var separatorIndex = protocol.IndexOf('/');
        if (separatorIndex >= 0 && separatorIndex < protocol.Length - 1)
        {
            return protocol[(separatorIndex + 1)..];
        }

        return protocol;
    }

    private static double? GetElapsedMilliseconds(HttpContext context)
    {
        if (context.Items[RequestTimingMiddleware.StartTimestampItemKey] is not long startTimestamp)
        {
            return null;
        }

        return Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
    }
}
