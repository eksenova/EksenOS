using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Eksen.Telemetry.AspNetCore.Middlewares;

/// <summary>
/// Paths whose request bodies are never captured (for example payment callbacks, authentication and
/// third-party integrations), in addition to <see cref="EksenTelemetryOptions.ExcludedPaths"/>.
/// </summary>
public sealed class RequestBodyCaptureOptions
{
    /// <summary>Path prefixes, compared case-insensitively.</summary>
    public List<string> ExcludedPrefixes { get; } = [];

    /// <summary>
    /// Predicates on the request path for routes a prefix cannot describe, such as a route whose first segment is
    /// a secret. A path for which any predicate returns <see langword="true"/> is never captured.
    /// </summary>
    public List<Func<string, bool>> ExcludedPathPredicates { get; } = [];

    public bool IsExcluded(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        foreach (var excluded in ExcludedPrefixes)
        {
            if (!string.IsNullOrEmpty(excluded) && path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var predicate in ExcludedPathPredicates)
        {
            if (predicate(path))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Buffers small textual request bodies (JSON, XML, form, text) of POST, PUT, PATCH and DELETE requests so the
/// completion event can carry them. Active only when <see cref="EksenTelemetryOptions.CaptureRequestBodies"/> is set.
/// </summary>
public sealed class RequestBodyCaptureMiddleware(
    IOptionsMonitor<EksenTelemetryOptions> telemetryOptions,
    IOptionsMonitor<RequestBodyCaptureOptions> captureOptions
) : IMiddleware
{
    public const string HttpContextItemKey = "Eksen.Telemetry.RequestBody";
    public const string TruncationMarker = "...[truncated]";

    private static readonly string[] CapturedMethods =
    [
        HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete
    ];

    private static readonly string[] CapturedContentTypePrefixes =
    [
        "application/json",
        "application/xml",
        "application/x-www-form-urlencoded",
        "text/"
    ];

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var options = telemetryOptions.CurrentValue;
        if (options.CaptureRequestBodies && ShouldCapture(context, options))
        {
            await CaptureAsync(context, options.MaxRequestBodyBytes);
        }

        await next(context);
    }

    private static async Task CaptureAsync(HttpContext context, int maxBytes)
    {
        context.Request.EnableBuffering(bufferThreshold: maxBytes, bufferLimit: maxBytes);

        var buffer = new byte[maxBytes];
        var totalRead = 0;
        int read;
        while (totalRead < buffer.Length
               && (read = await context.Request.Body.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), context.RequestAborted)) > 0)
        {
            totalRead += read;
        }

        context.Request.Body.Position = 0;

        if (totalRead > 0)
        {
            var truncated = totalRead == buffer.Length && context.Request.ContentLength > buffer.Length;
            var body = Encoding.UTF8.GetString(buffer, 0, totalRead);
            context.Items[HttpContextItemKey] = truncated ? body + TruncationMarker : body;
        }
    }

    private bool ShouldCapture(HttpContext context, EksenTelemetryOptions options)
    {
        if (options.MaxRequestBodyBytes <= 0)
        {
            return false;
        }

        var request = context.Request;
        if (!CapturedMethods.Contains(request.Method, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = request.Path.Value;
        if (options.IsExcludedPath(path))
        {
            return false;
        }

        if (captureOptions.CurrentValue.IsExcluded(path))
        {
            return false;
        }

        if (request.ContentLength is null or 0 or < 0 || request.ContentLength > options.MaxRequestBodyBytes)
        {
            return false;
        }

        var contentType = request.ContentType;
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        foreach (var prefix in CapturedContentTypePrefixes)
        {
            if (contentType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
