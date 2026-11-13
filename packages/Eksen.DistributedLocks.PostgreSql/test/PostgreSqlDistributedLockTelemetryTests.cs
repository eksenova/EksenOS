using System.Diagnostics.Metrics;
using Eksen.Telemetry;
using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Eksen.DistributedLocks.PostgreSql.Tests;

[Collection(PostgreSqlCollection.Name)]
public class PostgreSqlDistributedLockTelemetryTests(PostgreSqlFixture fixture) : EksenUnitTestBase
{
    private readonly IMeterFactory _meterFactory =
        new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();

    private PostgreSqlDistributedLockProvider CreateProvider()
    {
        return new PostgreSqlDistributedLockProvider(
            Options.Create(new PostgreSqlDistributedLockOptions { ConnectionString = fixture.ConnectionString }),
            Options.Create(new EksenDistributedLockOptions()),
            DistributedLocksTelemetry.For(_meterFactory));
    }

    [Fact]
    public async Task Acquire_And_Release_Record_Wait_Acquisition_And_Hold()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.DistributedLocks, _meterFactory);
        var provider = CreateProvider();

        var handle = await provider.AcquireAsync("telemetry-lock-1", cancellationToken: TestContext.Current.CancellationToken);
        collector.MeasurementsOf("eksen.lock.hold.duration").ShouldBeEmpty();
        await handle.DisposeAsync();

        var acquisition = collector.MeasurementsOf("eksen.lock.acquisitions").ShouldHaveSingleItem();
        acquisition.Tag("result").ShouldBe("acquired");
        acquisition.Tag("eksen.lock.name").ShouldBe("telemetry-lock-1");
        collector.MeasurementsOf("eksen.lock.wait.duration").ShouldHaveSingleItem().Tag("result").ShouldBe("acquired");
        collector.MeasurementsOf("eksen.lock.hold.duration").ShouldHaveSingleItem().Tag("eksen.lock.name").ShouldBe("telemetry-lock-1");
    }

    [Fact]
    public async Task Contested_TryAcquire_Records_Not_Acquired_And_Timeout()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.DistributedLocks, _meterFactory);
        var provider = CreateProvider();
        await using var holder = await provider.AcquireAsync("telemetry-lock-2", cancellationToken: TestContext.Current.CancellationToken);

        var immediate = await provider.TryAcquireAsync("telemetry-lock-2", cancellationToken: TestContext.Current.CancellationToken);
        var waited = await provider.TryAcquireAsync("telemetry-lock-2", TimeSpan.FromMilliseconds(150), TestContext.Current.CancellationToken);

        immediate.IsAcquired.ShouldBeFalse();
        waited.IsAcquired.ShouldBeFalse();
        collector.MeasurementsOf("eksen.lock.acquisitions").Select(measurement => (string?)measurement.Tag("result"))
            .ShouldBe(["acquired", "not_acquired", "timeout"]);
        collector.MeasurementsOf("eksen.lock.wait.duration").Last().Value.ShouldBeGreaterThanOrEqualTo(0.1);
    }

    [Fact]
    public async Task Acquire_Timeout_Is_Recorded_Before_The_Exception()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.DistributedLocks, _meterFactory);
        var provider = CreateProvider();
        await using var holder = await provider.AcquireAsync("telemetry-lock-3", cancellationToken: TestContext.Current.CancellationToken);

        await Should.ThrowAsync<DistributedLockException>(() =>
            provider.AcquireAsync("telemetry-lock-3", TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken));

        collector.MeasurementsOf("eksen.lock.acquisitions").Last().Tag("result").ShouldBe("timeout");
    }

    [Fact]
    public async Task Per_Entity_Lock_Names_Share_One_Metric_Series()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.DistributedLocks, _meterFactory);
        var provider = CreateProvider();

        await (await provider.AcquireAsync("order:123", cancellationToken: TestContext.Current.CancellationToken)).DisposeAsync();
        await (await provider.AcquireAsync("order:456", cancellationToken: TestContext.Current.CancellationToken)).DisposeAsync();

        collector.MeasurementsOf("eksen.lock.acquisitions").Select(measurement => (string?)measurement.Tag("eksen.lock.name"))
            .ShouldBe(["order", "order"]);
        collector.MeasurementsOf("eksen.lock.hold.duration").Select(measurement => (string?)measurement.Tag("eksen.lock.name"))
            .ShouldBe(["order", "order"]);
    }

    [Fact]
    public async Task Anonymous_Locks_Share_One_Metric_Name()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.DistributedLocks, _meterFactory);
        var provider = CreateProvider();

        await using (await provider.AcquireAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
        }

        collector.MeasurementsOf("eksen.lock.acquisitions").ShouldHaveSingleItem().Tag("eksen.lock.name").ShouldBe("anonymous");
    }
}
