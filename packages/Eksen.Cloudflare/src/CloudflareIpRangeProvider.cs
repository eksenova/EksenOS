using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.Cloudflare;

public sealed class CloudflareIpRangeProvider(
    ICloudflareIpRangeSource source,
    ICloudflareIpRangeStore store,
    IOptions<EksenCloudflareOptions> options,
    ILogger<CloudflareIpRangeProvider> logger,
    TimeProvider timeProvider
) : ICloudflareIpRangeProvider
{
    private readonly SemaphoreSlim refreshLock = new(initialCount: 1, maxCount: 1);

    private volatile CloudflareIpRanges current = CloudflareIpRanges.Empty;

    public CloudflareIpRanges Current
    {
        get { return current; }
    }

    public bool IsLoaded
    {
        get { return !current.IsEmpty; }
    }

    public async Task<CloudflareIpRanges> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await refreshLock.WaitAsync(cancellationToken);

        try
        {
            return await RefreshCoreAsync(cancellationToken);
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private async Task<CloudflareIpRanges> RefreshCoreAsync(CancellationToken cancellationToken)
    {
        var value = options.Value;

        var fetched = await TryFetchAsync(value.MaxFetchAttempts, cancellationToken);

        if (fetched is not null)
        {
            await PublishAsync(fetched, cancellationToken);
            return fetched;
        }

        var cached = await TryReadStoreAsync(cancellationToken);

        if (cached is not null)
        {
            logger.LogWarning(
                "Cloudflare IP ranges could not be fetched after {AttemptCount} attempts. "
                + "Falling back to the cached snapshot of {NetworkCount} networks.",
                value.MaxFetchAttempts,
                cached.Networks.Length);

            current = cached;
            return cached;
        }

        if (IsLoaded)
        {
            logger.LogWarning(
                "Cloudflare IP ranges could not be refreshed and no cached snapshot is available. "
                + "Keeping the {NetworkCount} networks already in memory.",
                current.Networks.Length);

            return current;
        }

        return await RetryUntilLoadedAsync(value, cancellationToken);
    }

    private async Task<CloudflareIpRanges> RetryUntilLoadedAsync(
        EksenCloudflareOptions value,
        CancellationToken cancellationToken)
    {
        var deadline = value.UncachedRetryTimeout is null
            ? (DateTimeOffset?)null
            : timeProvider.GetUtcNow().Add(value.UncachedRetryTimeout.Value);

        logger.LogError(
            "Cloudflare IP ranges are neither fetchable nor cached. Retrying {RetryScope}.",
            deadline is null ? "indefinitely" : $"until {deadline:O}");

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(value.FetchRetryDelay, timeProvider, cancellationToken);

            var fetched = await TryFetchAsync(value.MaxFetchAttempts, cancellationToken);

            if (fetched is not null)
            {
                await PublishAsync(fetched, cancellationToken);
                return fetched;
            }

            var cached = await TryReadStoreAsync(cancellationToken);

            if (cached is not null)
            {
                current = cached;
                return cached;
            }

            if (deadline is not null && timeProvider.GetUtcNow() >= deadline)
            {
                logger.LogError(
                    "Giving up on Cloudflare IP ranges after the configured retry window of {RetryWindow}.",
                    value.UncachedRetryTimeout);

                return CloudflareIpRanges.Empty;
            }
        }

        return CloudflareIpRanges.Empty;
    }

    private async Task PublishAsync(CloudflareIpRanges ranges, CancellationToken cancellationToken)
    {
        current = ranges;

        try
        {
            await store.WriteAsync(ranges, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Cloudflare IP ranges were fetched but could not be written to the cache store.");
        }
    }

    private async Task<CloudflareIpRanges?> TryFetchAsync(int maxAttempts, CancellationToken cancellationToken)
    {
        var value = options.Value;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await source.FetchAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Fetching Cloudflare IP ranges failed on attempt {Attempt} of {MaxAttempts}.",
                    attempt,
                    maxAttempts);

                if (attempt < maxAttempts)
                {
                    await Task.Delay(value.FetchRetryDelay, timeProvider, cancellationToken);
                }
            }
        }

        return null;
    }

    private async Task<CloudflareIpRanges?> TryReadStoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cached = await store.ReadAsync(cancellationToken);

            if (cached is null || cached.IsEmpty)
            {
                return null;
            }

            return cached;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Reading cached Cloudflare IP ranges failed.");
            return null;
        }
    }
}
