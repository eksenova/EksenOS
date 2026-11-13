using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Eksen.Telemetry.AspNetCore.Health;

public static class EksenHealthChecks
{
    public const string LivePath = "/healthz/live";
    public const string ReadyPath = "/healthz/ready";
    public const string LiveTag = "live";
    public const string ReadyTag = "ready";

    internal static readonly HealthCheckOptions LiveOptions = new()
    {
        Predicate = registration => registration.Tags.Contains(LiveTag),
        ResponseWriter = WritePlainStatusAsync,
        AllowCachingResponses = false
    };

    internal static readonly HealthCheckOptions ReadyOptions = new()
    {
        Predicate = registration => registration.Tags.Contains(ReadyTag),
        ResponseWriter = WritePlainStatusAsync,
        AllowCachingResponses = false
    };

    public static bool IsHealthPath(PathString path)
    {
        return path.Equals(LivePath, StringComparison.OrdinalIgnoreCase)
               || path.Equals(ReadyPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Wraps the endpoint so it answers 404 unless the connection arrived on <paramref name="managementPort"/>. The
    /// <c>Host</c> header is not used on a real listener: behind a port mapping (<c>docker run -p 18081:8081</c>, a node
    /// port, a proxy) it names another port, and on the public listener it could be spoofed. A local port of 0 means
    /// an in-memory server with no listener (the test server); only there the port in the <c>Host</c> header decides.
    /// </summary>
    internal static void RequireLocalPort(EndpointBuilder builder, int managementPort)
    {
        var inner = builder.RequestDelegate;
        if (inner is null)
        {
            return;
        }

        builder.RequestDelegate = context =>
        {
            var localPort = context.Connection.LocalPort;
            var port = localPort != 0 ? localPort : context.Request.Host.Port;
            if (port != managementPort)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }

            return inner(context);
        };
    }

    /// <summary>
    /// Builds the listen URLs that keep the application's current endpoints and add
    /// <c>http://*:<paramref name="managementPort"/></c>.
    /// </summary>
    public static string[] BuildListenUrls(string? urls, string? httpPorts, string? httpsPorts, int managementPort)
    {
        var result = new List<string>();

        if (!string.IsNullOrWhiteSpace(urls))
        {
            result.AddRange(Split(urls));
        }
        else
        {
            foreach (var port in Split(httpPorts))
            {
                result.Add($"http://*:{port}");
            }

            foreach (var port in Split(httpsPorts))
            {
                result.Add($"https://*:{port}");
            }

            if (result.Count == 0)
            {
                // Kestrel's own default when nothing is configured.
                result.Add("http://localhost:5000");
            }
        }

        var managementUrl = $"http://*:{managementPort}";
        if (!result.Any(url => url.EndsWith(":" + managementPort, StringComparison.Ordinal)
                               || url.EndsWith(":" + managementPort + "/", StringComparison.Ordinal)))
        {
            result.Add(managementUrl);
        }

        return result.ToArray();
    }

    private static IEnumerable<string> Split(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static Task WritePlainStatusAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsync(report.Status.ToString());
    }
}
