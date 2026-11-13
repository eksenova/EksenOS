using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Eksen.Telemetry.AspNetCore.Middlewares;

/// <summary>
/// Stores the request start timestamp in <see cref="HttpContext.Items"/> for <c>eksen.http.elapsed_ms</c>.
/// </summary>
public sealed class RequestTimingMiddleware : IMiddleware
{
    public const string StartTimestampItemKey = "Eksen.Telemetry.RequestStartTimestamp";

    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Items[StartTimestampItemKey] = Stopwatch.GetTimestamp();
        return next(context);
    }
}
