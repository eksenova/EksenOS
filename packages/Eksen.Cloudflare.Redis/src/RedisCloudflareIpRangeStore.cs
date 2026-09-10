using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Eksen.Cloudflare.Redis;

public sealed class RedisCloudflareIpRangeStore(
    IConnectionMultiplexer connectionMultiplexer,
    IOptions<RedisCloudflareOptions> options
) : ICloudflareIpRangeStore
{
    public async Task<CloudflareIpRanges?> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var database = connectionMultiplexer.GetDatabase();
        var payload = await database.StringGetAsync(options.Value.Key);

        if (payload.IsNullOrEmpty)
        {
            return null;
        }

        var ranges = CloudflareIpRanges.Parse(
            ((string)payload!).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (ranges.IsEmpty)
        {
            return null;
        }

        return ranges;
    }

    public async Task WriteAsync(CloudflareIpRanges ranges, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        cancellationToken.ThrowIfCancellationRequested();

        if (ranges.IsEmpty)
        {
            return;
        }

        var payload = string.Join('\n', ranges.Networks.Select(network => network.ToString()));
        var database = connectionMultiplexer.GetDatabase();

        await database.StringSetAsync(options.Value.Key, payload, options.Value.Expiry);
    }
}
