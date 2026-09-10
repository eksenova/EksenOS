namespace Eksen.Cloudflare;

public static class CloudflareDefaults
{
    public const string ConnectingIpHeaderName = "CF-Connecting-IP";

    public const string ForwardedProtoHeaderName = "X-Forwarded-Proto";

    public const string RayHeaderName = "CF-Ray";

    public const string CountryHeaderName = "CF-IPCountry";

    public const string IpV4ListUrl = "https://www.cloudflare.com/ips-v4";

    public const string IpV6ListUrl = "https://www.cloudflare.com/ips-v6";

    public const string HttpClientName = "Eksen.Cloudflare";

    public const string TurnstileVerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public const string TurnstileHttpClientName = "Eksen.Cloudflare.Turnstile";

    public const string OriginalRemoteIpAddressItemKey = "Eksen.Cloudflare.OriginalRemoteIpAddress";

    public const string OriginalSchemeItemKey = "Eksen.Cloudflare.OriginalScheme";
}
