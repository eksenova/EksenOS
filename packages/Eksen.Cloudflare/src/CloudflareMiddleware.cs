using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.Cloudflare;

public sealed class CloudflareMiddleware(
    RequestDelegate next,
    ICloudflareIpRangeProvider rangeProvider,
    IOptions<EksenCloudflareOptions> options,
    ILogger<CloudflareMiddleware> logger
)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var value = options.Value;
        var connectingAddress = context.Connection.RemoteIpAddress;

        if (rangeProvider.Current.Contains(connectingAddress))
        {
            if (value.RestoreConnectingIp)
            {
                RestoreConnectingIp(context, value, connectingAddress);
            }

            if (value.RestoreForwardedProto)
            {
                RestoreForwardedProto(context, value);
            }
        }

        await next(context);
    }

    private void RestoreConnectingIp(
        HttpContext context,
        EksenCloudflareOptions value,
        IPAddress? connectingAddress)
    {
        var headerValue = context.Request.Headers[value.ConnectingIpHeaderName].ToString();

        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return;
        }

        if (!IPAddress.TryParse(headerValue.Trim(), out var clientAddress))
        {
            logger.LogDebug(
                "Ignoring unparsable {HeaderName} value received from a Cloudflare address.",
                value.ConnectingIpHeaderName);

            return;
        }

        context.Items[CloudflareDefaults.OriginalRemoteIpAddressItemKey] = connectingAddress;
        context.Connection.RemoteIpAddress = CloudflareIpRanges.Normalize(clientAddress);
    }

    private static void RestoreForwardedProto(HttpContext context, EksenCloudflareOptions value)
    {
        var headerValue = context.Request.Headers[value.ForwardedProtoHeaderName].ToString();

        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return;
        }

        var separatorIndex = headerValue.IndexOf(',');
        var scheme = separatorIndex >= 0
            ? headerValue[..separatorIndex].Trim()
            : headerValue.Trim();

        if (!string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        context.Items[CloudflareDefaults.OriginalSchemeItemKey] = context.Request.Scheme;
        context.Request.Scheme = scheme.ToLowerInvariant();
    }
}
