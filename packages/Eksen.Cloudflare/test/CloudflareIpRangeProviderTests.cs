using Eksen.Cloudflare;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Eksen.Cloudflare.Tests;

public sealed class CloudflareIpRangeProviderTests
{
    private static CloudflareIpRangeProvider CreateProvider(
        ICloudflareIpRangeSource source,
        ICloudflareIpRangeStore store,
        EksenCloudflareOptions options)
    {
        return new CloudflareIpRangeProvider(
            source,
            store,
            Options.Create(options),
            NullLogger<CloudflareIpRangeProvider>.Instance,
            TimeProvider.System);
    }

    private static EksenCloudflareOptions FastOptions(int maxAttempts = 3)
    {
        return new EksenCloudflareOptions
        {
            MaxFetchAttempts = maxAttempts,
            FetchRetryDelay = TimeSpan.FromMilliseconds(1),
        };
    }

    [Fact]
    public async Task RefreshAsync_Should_Publish_And_Cache_On_Success()
    {
        var source = new FakeCloudflareIpRangeSource(true);
        var store = new FakeCloudflareIpRangeStore();
        var provider = CreateProvider(source, store, FastOptions());

        var result = await provider.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.False(result.IsEmpty);
        Assert.True(provider.IsLoaded);
        Assert.Equal(1, source.AttemptCount);
        Assert.NotNull(store.Written);
    }

    [Fact]
    public async Task RefreshAsync_Should_Retry_Up_To_MaxFetchAttempts_Before_Falling_Back()
    {
        var source = new FakeCloudflareIpRangeSource(false);
        var store = new FakeCloudflareIpRangeStore(FakeCloudflareIpRangeSource.SampleRanges);
        var provider = CreateProvider(source, store, FastOptions(maxAttempts: 3));

        var result = await provider.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, source.AttemptCount);
        Assert.False(result.IsEmpty);
        Assert.True(provider.IsLoaded);
    }

    [Fact]
    public async Task RefreshAsync_Should_Succeed_On_A_Later_Attempt_Without_Touching_The_Cache()
    {
        var source = new FakeCloudflareIpRangeSource(false, false, true);
        var store = new FakeCloudflareIpRangeStore();
        var provider = CreateProvider(source, store, FastOptions(maxAttempts: 3));

        var result = await provider.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, source.AttemptCount);
        Assert.False(result.IsEmpty);
        Assert.NotNull(store.Written);
    }

    [Fact]
    public async Task RefreshAsync_Should_Give_Up_When_UncachedRetryTimeout_Elapses()
    {
        var source = new FakeCloudflareIpRangeSource(false);
        var store = new FakeCloudflareIpRangeStore();
        var options = FastOptions(maxAttempts: 1);
        options.UncachedRetryTimeout = TimeSpan.FromMilliseconds(50);

        var provider = CreateProvider(source, store, options);

        var result = await provider.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsEmpty);
        Assert.False(provider.IsLoaded);
    }

    // A null UncachedRetryTimeout means "keep retrying forever": the provider must not give up
    // before the source finally starts succeeding.
    [Fact]
    public async Task RefreshAsync_Should_Retry_Indefinitely_When_UncachedRetryTimeout_Is_Null()
    {
        var attemptsBeforeSuccess = Enumerable.Repeat(false, 12).Append(true).ToArray();
        var source = new FakeCloudflareIpRangeSource(attemptsBeforeSuccess);
        var store = new FakeCloudflareIpRangeStore();
        var options = FastOptions(maxAttempts: 2);
        options.UncachedRetryTimeout = null;

        var provider = CreateProvider(source, store, options);

        var result = await provider.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.False(result.IsEmpty);
        Assert.True(provider.IsLoaded);
        Assert.True(source.AttemptCount >= 13);
    }

    [Fact]
    public async Task RefreshAsync_Should_Keep_Retrying_When_Cache_Read_Throws()
    {
        var source = new FakeCloudflareIpRangeSource(false, false, false, true);
        var store = new FakeCloudflareIpRangeStore { ThrowOnRead = true };
        var options = FastOptions(maxAttempts: 2);
        options.UncachedRetryTimeout = null;

        var provider = CreateProvider(source, store, options);

        var result = await provider.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.False(result.IsEmpty);
    }

    [Fact]
    public async Task RefreshAsync_Should_Keep_Existing_Ranges_When_A_Later_Refresh_Fails()
    {
        var source = new FakeCloudflareIpRangeSource(true, false);
        var store = new FakeCloudflareIpRangeStore();
        var provider = CreateProvider(source, store, FastOptions(maxAttempts: 1));

        await provider.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.True(provider.IsLoaded);

        var second = await provider.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.False(second.IsEmpty);
        Assert.True(provider.IsLoaded);
    }
}
