namespace Eksen.Cloudflare;

public sealed class InMemoryCloudflareIpRangeStore : ICloudflareIpRangeStore
{
    private volatile CloudflareIpRanges? snapshot;

    public Task<CloudflareIpRanges?> ReadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(snapshot);
    }

    public Task WriteAsync(CloudflareIpRanges ranges, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        snapshot = ranges;
        return Task.CompletedTask;
    }
}
