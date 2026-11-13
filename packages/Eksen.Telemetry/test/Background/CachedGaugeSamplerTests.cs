using Eksen.Telemetry.Background;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Telemetry.Tests.Background;

public class CachedGaugeSamplerTests : EksenUnitTestBase
{
    [Fact]
    public async Task Value_Should_Only_Change_When_Refreshed()
    {
        // Arrange
        var calls = 0;
        var sampler = new CachedGaugeSampler<int>("pending", _ => Task.FromResult(Interlocked.Increment(ref calls)));

        // Assert initial state
        sampler.HasValue.ShouldBeFalse();
        sampler.TryGetValue(out _).ShouldBeFalse();
        sampler.Interval.ShouldBe(TimeSpan.FromSeconds(15));

        // Act
        (await sampler.RefreshAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();

        // Assert: reading never triggers a refresh
        sampler.Value.ShouldBe(1);
        sampler.Value.ShouldBe(1);
        calls.ShouldBe(1);
        sampler.LastRefreshedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_Failed_Refresh_Should_Keep_The_Previous_Value()
    {
        var fail = false;
        var sampler = new CachedGaugeSampler<int>("pending", _ => fail ? throw new InvalidOperationException() : Task.FromResult(7));

        await sampler.RefreshAsync(TestContext.Current.CancellationToken);
        fail = true;

        (await sampler.RefreshAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        sampler.Value.ShouldBe(7);
    }

    [Fact]
    public async Task The_Hosted_Loop_Should_Refresh_Periodically_Until_Stopped()
    {
        var calls = 0;
        var sampler = new CachedGaugeSampler<int>(
            "pending",
            _ => Task.FromResult(Interlocked.Increment(ref calls)),
            TimeSpan.FromMilliseconds(20));

        await sampler.StartAsync(TestContext.Current.CancellationToken);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Volatile.Read(ref calls) < 3 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        await sampler.StopAsync(TestContext.Current.CancellationToken);

        Volatile.Read(ref calls).ShouldBeGreaterThanOrEqualTo(3);
        sampler.HasValue.ShouldBeTrue();
    }

    [Fact]
    public void A_Non_Positive_Interval_Should_Be_Rejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new CachedGaugeSampler<int>("pending", _ => Task.FromResult(0), TimeSpan.Zero));
    }
}
