using Eksen.Cloudflare;

namespace Eksen.Cloudflare.Tests;

/// <summary>
/// Source whose outcome is scripted per attempt so retry behaviour can be asserted deterministically.
/// </summary>
public sealed class FakeCloudflareIpRangeSource(params bool[] attemptSucceeds) : ICloudflareIpRangeSource
{
    public static readonly CloudflareIpRanges SampleRanges =
        CloudflareIpRanges.Parse(["203.0.113.0/24", "2001:db8::/32"]);

    private int attemptCount;

    public int AttemptCount
    {
        get { return Volatile.Read(ref attemptCount); }
    }

    public Task<CloudflareIpRanges> FetchAsync(CancellationToken cancellationToken = default)
    {
        var attempt = Interlocked.Increment(ref attemptCount);

        var succeeds = attemptSucceeds.Length == 0
            ? false
            : attemptSucceeds[Math.Min(attempt - 1, attemptSucceeds.Length - 1)];

        if (!succeeds)
        {
            throw new CloudflareIpRangeException($"Scripted failure on attempt {attempt}.");
        }

        return Task.FromResult(SampleRanges);
    }
}

/// <summary>
/// Store that can be pre-seeded, or made to throw, to exercise the cache-fallback path.
/// </summary>
public sealed class FakeCloudflareIpRangeStore(CloudflareIpRanges? seeded = null) : ICloudflareIpRangeStore
{
    public CloudflareIpRanges? Written { get; private set; }

    public bool ThrowOnRead { get; set; }

    public Task<CloudflareIpRanges?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (ThrowOnRead)
        {
            throw new InvalidOperationException("Cache is unavailable.");
        }

        return Task.FromResult(seeded);
    }

    public Task WriteAsync(CloudflareIpRanges ranges, CancellationToken cancellationToken = default)
    {
        Written = ranges;
        return Task.CompletedTask;
    }
}
