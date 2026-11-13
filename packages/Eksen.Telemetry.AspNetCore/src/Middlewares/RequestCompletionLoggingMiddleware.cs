using System.Collections;
using System.Diagnostics;
using Eksen.Telemetry.AspNetCore.Enrichment;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.Telemetry.AspNetCore.Middlewares;

/// <summary>
/// Writes one event per request when it ends, under category <c>Eksen.Telemetry.RequestLog</c>: Information
/// below 500, Error from 500. The event carries every request, response, route and contributor field as
/// structured attributes.
/// </summary>
public sealed class RequestCompletionLoggingMiddleware(
    HttpRequestEnrichmentCollector collector,
    ILoggerFactory loggerFactory,
    IOptionsMonitor<EksenTelemetryOptions> telemetryOptions
) : IMiddleware
{
    public const string MessageTemplate =
        "HTTP {http.request.method} {url.path} ended with {http.response.status_code} in {http.server.request.duration_ms} ms";

    public const string DurationProperty = "http.server.request.duration_ms";

    private static readonly EventId CompletedEventId = new(1, "RequestCompleted");

    private readonly ILogger _logger = loggerFactory.CreateLogger(EksenTelemetry.RequestLogCategory);

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (telemetryOptions.CurrentValue.IsExcludedPath(context.Request.Path.Value))
        {
            await next(context);
            return;
        }

        var startTimestamp = Stopwatch.GetTimestamp();
        Exception? failure = null;
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            Write(context, Stopwatch.GetElapsedTime(startTimestamp), failure);
        }
    }

    private void Write(HttpContext context, TimeSpan elapsed, Exception? failure)
    {
        try
        {
            var statusCode = failure is not null && !context.Response.HasStarted
                ? StatusCodes.Status500InternalServerError
                : context.Response.StatusCode;

            var level = statusCode >= StatusCodes.Status500InternalServerError ? LogLevel.Error : LogLevel.Information;
            if (!_logger.IsEnabled(level))
            {
                return;
            }

            var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
            Add(fields, collector.CollectHttpRequest(context));
            Add(fields, collector.CollectHttpResponse(context));
            Add(fields, collector.CollectHttpRoute(context));
            Add(fields, RequestEnrichmentMiddleware.Collect(context));

            fields[EksenSemanticConventions.HttpResponseStatusCode] = statusCode;
            var durationMs = Math.Round(elapsed.TotalMilliseconds, 3);
            fields[DurationProperty] = durationMs;

            var state = new CompletionState(
                fields,
                context.Request.Method,
                fields.TryGetValue(EksenSemanticConventions.UrlPath, out var path) ? path : null,
                statusCode,
                durationMs);

            _logger.Log(level, CompletedEventId, state, failure, static (value, _) => value.ToString());
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Telemetry must never fail a request.
            _logger.LogDebug(exception, "Writing the request completion event failed");
        }
    }

    private static void Add(Dictionary<string, object?> fields, IEnumerable<RequestEnrichmentProperty> properties)
    {
        foreach (var property in properties)
        {
            if (!RequestEnrichmentValues.IsEmpty(property.Value))
            {
                fields[property.Name] = RequestLogScope.Redact(property.Name, property.Value);
            }
        }
    }

    /// <summary>
    /// Structured state: every field, then the template under <c>{OriginalFormat}</c>.
    /// </summary>
    private sealed class CompletionState(
        Dictionary<string, object?> fields,
        string method,
        object? path,
        int statusCode,
        double durationMs
    ) : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private readonly KeyValuePair<string, object?>[] _items = fields
            .Append(new KeyValuePair<string, object?>("{OriginalFormat}", MessageTemplate))
            .ToArray();

        public int Count
        {
            get { return _items.Length; }
        }

        public KeyValuePair<string, object?> this[int index]
        {
            get { return _items[index]; }
        }

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, object?>>)_items).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return $"HTTP {method} {path} ended with {statusCode} in {durationMs.ToString(System.Globalization.CultureInfo.InvariantCulture)} ms";
        }
    }
}
