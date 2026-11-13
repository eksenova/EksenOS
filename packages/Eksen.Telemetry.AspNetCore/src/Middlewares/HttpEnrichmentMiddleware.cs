using Eksen.Telemetry.AspNetCore.Enrichment;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.Telemetry.AspNetCore.Middlewares;

/// <summary>
/// Puts the request fields (method, scrubbed path and URL, redacted query and headers, trace ids) in the log
/// scope of everything logged while the request runs. The captured request body is left out of the scope: it is
/// written once, on the request completion event.
/// </summary>
public sealed class HttpEnrichmentMiddleware(
    HttpRequestEnrichmentCollector collector,
    ILoggerFactory loggerFactory,
    IOptionsMonitor<EksenTelemetryOptions> telemetryOptions
) : IMiddleware
{
    private readonly ILogger _logger = loggerFactory.CreateLogger(EksenTelemetry.RequestLogCategory);

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (telemetryOptions.CurrentValue.IsExcludedPath(context.Request.Path.Value))
        {
            await next(context);
            return;
        }

        using (RequestLogScope.Begin(_logger, collector.CollectHttpRequest(context, includeBody: false)))
        {
            await next(context);
        }
    }
}

/// <summary>
/// Adds the matched route (template, controller, action, endpoint name and route values) to the log scope.
/// Register after <c>UseRouting</c>.
/// </summary>
public sealed class HttpRouteEnrichmentMiddleware(
    HttpRequestEnrichmentCollector collector,
    ILoggerFactory loggerFactory,
    IOptionsMonitor<EksenTelemetryOptions> telemetryOptions
) : IMiddleware
{
    private readonly ILogger _logger = loggerFactory.CreateLogger(EksenTelemetry.RequestLogCategory);

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (telemetryOptions.CurrentValue.IsExcludedPath(context.Request.Path.Value))
        {
            await next(context);
            return;
        }

        using (RequestLogScope.Begin(_logger, collector.CollectHttpRoute(context)))
        {
            await next(context);
        }
    }
}
