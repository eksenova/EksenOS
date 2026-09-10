namespace Eksen.Cloudflare;

public interface ICloudflareIpRangeProvider
{
    CloudflareIpRanges Current { get; }

    bool IsLoaded { get; }

    Task<CloudflareIpRanges> RefreshAsync(CancellationToken cancellationToken = default);
}
