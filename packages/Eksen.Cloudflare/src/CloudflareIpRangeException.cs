namespace Eksen.Cloudflare;

public sealed class CloudflareIpRangeException : Exception
{
    public CloudflareIpRangeException(string message) : base(message)
    {
    }

    public CloudflareIpRangeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
