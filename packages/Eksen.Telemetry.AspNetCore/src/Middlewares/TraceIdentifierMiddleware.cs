using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Eksen.Telemetry.AspNetCore.Middlewares;

/// <summary>
/// Uses the W3C trace id as <see cref="HttpContext.TraceIdentifier"/> and returns it as <c>X-Correlation-Id</c>,
/// with the server span id as <c>X-Request-Id</c>. Removes <c>X-Powered-By</c>.
/// </summary>
public sealed class TraceIdentifierMiddleware : IMiddleware
{
    public const string CorrelationIdHeader = "X-Correlation-Id";
    public const string RequestIdHeader = "X-Request-Id";
    public const string SpanIdItemKey = "Eksen.Telemetry.SpanId";

    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var activity = Activity.Current;
        if (activity is not null && activity.IdFormat == ActivityIdFormat.W3C)
        {
            context.TraceIdentifier = activity.TraceId.ToHexString();
            context.Items[SpanIdItemKey] = activity.SpanId.ToHexString();
        }

        context.Response.OnStarting(static state =>
        {
            var httpContext = (HttpContext)state;
            httpContext.Response.Headers.Remove("X-Powered-By");

            if (!string.IsNullOrEmpty(httpContext.TraceIdentifier))
            {
                httpContext.Response.Headers[CorrelationIdHeader] = httpContext.TraceIdentifier;
            }

            if (httpContext.Items[SpanIdItemKey] is string spanId)
            {
                httpContext.Response.Headers[RequestIdHeader] = spanId;
            }

            return Task.CompletedTask;
        }, context);

        return next(context);
    }
}
