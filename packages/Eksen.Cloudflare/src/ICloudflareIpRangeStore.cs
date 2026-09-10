namespace Eksen.Cloudflare;

public interface ICloudflareIpRangeStore
{
    Task<CloudflareIpRanges?> ReadAsync(CancellationToken cancellationToken = default);

    Task WriteAsync(CloudflareIpRanges ranges, CancellationToken cancellationToken = default);
}
