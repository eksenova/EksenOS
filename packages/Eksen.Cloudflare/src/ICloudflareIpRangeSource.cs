namespace Eksen.Cloudflare;

public interface ICloudflareIpRangeSource
{
    Task<CloudflareIpRanges> FetchAsync(CancellationToken cancellationToken = default);
}
