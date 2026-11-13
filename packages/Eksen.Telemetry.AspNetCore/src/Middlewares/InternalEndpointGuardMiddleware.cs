using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Eksen.Telemetry.AspNetCore.Middlewares;

public sealed class InternalEndpointGuardOptions
{
    /// <summary>
    /// Path prefixes only callers from <see cref="AllowedNetworks"/> may reach.
    /// </summary>
    public List<string> GuardedPathPrefixes { get; } = [];

    /// <summary>
    /// CIDR ranges allowed to reach guarded paths. Defaults to loopback only.
    /// </summary>
    public List<string> AllowedNetworks { get; } = ["127.0.0.0/8", "::1/128"];
}

/// <summary>
/// Answers 404 for guarded paths unless the remote address falls inside one of the allowed networks.
/// </summary>
public sealed class InternalEndpointGuardMiddleware(IOptionsMonitor<InternalEndpointGuardOptions> options) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var current = options.CurrentValue;
        var path = context.Request.Path.Value;

        if (string.IsNullOrEmpty(path) || !IsGuarded(path, current))
        {
            await next(context);
            return;
        }

        var remote = context.Connection.RemoteIpAddress;
        if (remote is null || !IsAllowed(remote, current))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    }

    public static bool IsAllowed(IPAddress address, InternalEndpointGuardOptions options)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        foreach (var network in options.AllowedNetworks)
        {
            if (IPNetwork.TryParse(network, out var parsed) && parsed.Contains(address))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGuarded(string path, InternalEndpointGuardOptions options)
    {
        foreach (var prefix in options.GuardedPathPrefixes)
        {
            if (!string.IsNullOrEmpty(prefix) && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
