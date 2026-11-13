using System.Diagnostics;
using Eksen.Telemetry.AspNetCore.Enrichment;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.Telemetry.AspNetCore.Middlewares;

/// <summary>
/// Runs every <see cref="IRequestEnrichmentContributor"/> and pushes their fields to the log scope and, when
/// requested, to the current span. Register after <c>UseAuthentication</c>.
/// </summary>
public sealed class RequestEnrichmentMiddleware(
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

        var properties = Collect(context);

        var activity = Activity.Current;
        if (activity is not null)
        {
            foreach (var property in properties)
            {
                if (property.ActivityTag && !RequestEnrichmentValues.IsEmpty(property.Value))
                {
                    activity.SetTag(property.Name, property.Value);
                }
            }
        }

        using (RequestLogScope.Begin(_logger, properties))
        {
            await next(context);
        }
    }

    internal static List<RequestEnrichmentProperty> Collect(HttpContext context)
    {
        var properties = new List<RequestEnrichmentProperty>();
        foreach (var contributor in context.RequestServices.GetServices<IRequestEnrichmentContributor>())
        {
            properties.AddRange(contributor.Collect(context));
        }

        return properties;
    }
}
