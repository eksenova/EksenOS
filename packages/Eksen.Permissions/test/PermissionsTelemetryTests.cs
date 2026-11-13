using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eksen.Identity;
using Eksen.Identity.Users;
using Eksen.Permissions.Caching;
using Eksen.Permissions.Tests.Fakes;
using Eksen.Telemetry;
using Eksen.TestBase;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Eksen.Permissions.Tests;

public class PermissionsTelemetryTests : EksenUnitTestBase, IDisposable
{
    private static readonly ActivitySource TestSource = new("Eksen.Permissions.Tests");

    // Each test gets its own meter factory, so the collector sees only this test's measurements.
    private readonly ServiceProvider _metrics = new ServiceCollection().AddMetrics().BuildServiceProvider();
    private readonly Mock<IPermissionStore> _permissionStore = new();
    private readonly InMemoryPermissionCache _cache;

    public PermissionsTelemetryTests()
    {
        _cache = new InMemoryPermissionCache(
            new Mock<IAuthContext>().Object,
            new Lazy<IPermissionStore>(() => _permissionStore.Object),
            new MemoryCache(new MemoryCacheOptions()),
            MeterFactory);
    }

    private IMeterFactory MeterFactory
    {
        get { return _metrics.GetRequiredService<IMeterFactory>(); }
    }

    public void Dispose()
    {
        _metrics.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Telemetry_Is_One_Instance_Per_Meter_Factory()
    {
        var telemetry = PermissionsTelemetry.For(MeterFactory);

        PermissionsTelemetry.For(MeterFactory).ShouldBeSameAs(telemetry);
        telemetry.Meter.Scope.ShouldBeSameAs(MeterFactory);
        telemetry.Meter.Name.ShouldBe(EksenTelemetry.Permissions);
        PermissionsTelemetry.For(null).ShouldBeSameAs(PermissionsTelemetry.Shared);
    }

    [Fact]
    public void Container_Registers_Telemetry_On_Its_Meter_Factory()
    {
        using var provider = new ServiceCollection()
            .AddMetrics()
            .AddSingleton(provider => PermissionsTelemetry.For(provider.GetService<IMeterFactory>()))
            .BuildServiceProvider();

        provider.GetRequiredService<PermissionsTelemetry>().Meter.Scope
            .ShouldBeSameAs(provider.GetRequiredService<IMeterFactory>());
    }

    [Fact]
    public async Task User_Permission_Lookups_Count_A_Miss_Then_A_Hit()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.Permissions, MeterFactory);
        var userId = new EksenUserId(System.Ulid.NewUlid());
        _permissionStore.Setup(s => s.GetUserPermissionsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PermissionDefinition(PermissionName.Create("Orders.Create"))]);

        using var request = TestSource.StartActivity("request")!;
        await _cache.GetPermissionsForUserAsync(userId, TestContext.Current.CancellationToken);
        await _cache.GetPermissionsForUserAsync(userId, TestContext.Current.CancellationToken);

        var requests = collector.MeasurementsOf("eksen.permissions.cache.requests");
        requests.Select(measurement => (string?)measurement.Tag("cache.result")).ShouldBe(["miss", "hit"]);
        requests.ShouldAllBe(measurement => (string?)measurement.Tag("key_kind") == "user");

        var load = collector.ActivitiesIn(request.TraceId).Single(activity => activity.DisplayName == "permissions load");
        load.ParentSpanId.ShouldBe(request.SpanId);
        load.GetTagItem("key_kind").ShouldBe("user");
    }

    [Fact]
    public async Task Definition_Lookups_Count_A_Miss_Then_A_Hit()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.Permissions, MeterFactory);
        _permissionStore.Setup(s => s.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PermissionDefinition(PermissionName.Create("Orders.Create"))]);

        await _cache.GetPermissionDefinitionsAsync(TestContext.Current.CancellationToken);
        await _cache.GetPermissionDefinitionsAsync(TestContext.Current.CancellationToken);

        var requests = collector.MeasurementsOf("eksen.permissions.cache.requests");
        requests.Select(measurement => (string?)measurement.Tag("cache.result")).ShouldBe(["miss", "hit"]);
        requests.ShouldAllBe(measurement => (string?)measurement.Tag("key_kind") == "definitions");
    }

    [Fact]
    public async Task Invalidations_Are_Counted_By_Key_Kind()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.Permissions, MeterFactory);

        await _cache.InvalidateForUserAsync(new EksenUserId(System.Ulid.NewUlid()), TestContext.Current.CancellationToken);
        await _cache.InvalidateForUserIdsAsync(
            [new EksenUserId(System.Ulid.NewUlid()), new EksenUserId(System.Ulid.NewUlid())],
            TestContext.Current.CancellationToken);

        var invalidations = collector.MeasurementsOf("eksen.permissions.cache.invalidations");
        invalidations.Where(measurement => (string?)measurement.Tag("key_kind") == "user").Sum(measurement => measurement.Value).ShouldBe(3);
        invalidations.Where(measurement => (string?)measurement.Tag("key_kind") == "definitions").Sum(measurement => measurement.Value).ShouldBe(1);
    }

    [Fact]
    public async Task Store_Failure_Marks_The_Load_Span_Failed()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.Permissions, MeterFactory);
        _permissionStore.Setup(s => s.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("db"));

        using var request = TestSource.StartActivity("request")!;
        await Should.ThrowAsync<TimeoutException>(() => _cache.GetPermissionDefinitionsAsync(TestContext.Current.CancellationToken));

        collector.ActivitiesIn(request.TraceId).Single(activity => activity.DisplayName == "permissions load")
            .Status.ShouldBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task Permission_Checks_Record_Their_Duration_On_The_Given_Meter_Factory()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.Permissions, MeterFactory);
        var permission = PermissionName.Create("Orders.Create");
        var permissionCache = new Mock<IPermissionCache>();
        permissionCache.Setup(cache => cache.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PermissionDefinition(permission)]);
        permissionCache.Setup(cache => cache.GetPermissionsForUserAsync(It.IsAny<EksenUserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var checker = new PermissionChecker<FakeUser, FakeTenant>(
            permissionCache.Object,
            new Mock<IEksenUserRepository<FakeUser, FakeTenant>>().Object,
            Options.Create(new EksenPermissionOptions()),
            new Mock<IAuthContext>().Object,
            MeterFactory);

        (await checker.HasPermissionAsync(new EksenUserId(System.Ulid.NewUlid()), permission)).ShouldBeFalse();

        collector.MeasurementsOf("eksen.permissions.check.duration")
            .Single().Tag("eksen.permissions.result").ShouldBe("denied");
    }
}
