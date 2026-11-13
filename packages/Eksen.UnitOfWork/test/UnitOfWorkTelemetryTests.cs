using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eksen.Telemetry;
using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;

namespace Eksen.UnitOfWork.Tests;

public class UnitOfWorkTelemetryTests : EksenUnitTestBase
{
    private static readonly ActivitySource TestSource = new("Eksen.UnitOfWork.Tests");

    private static (ServiceProvider Provider, IMeterFactory MeterFactory) CreateProvider(
        Action<EksenTelemetryOptions>? configureTelemetry = null)
    {
        var services = new ServiceCollection();
        services.AddEksen(eksen => eksen.AddUnitOfWork());
        if (configureTelemetry != null)
        {
            services.Configure(configureTelemetry);
        }

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        return (provider, provider.GetRequiredService<IMeterFactory>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Commit_Records_The_Commit_Outcome_And_Adds_A_Span_Event(bool isTransactional)
    {
        var (provider, meterFactory) = CreateProvider();
        await using var _ = provider;
        using var collector = new TelemetryCollector(EksenTelemetry.UnitOfWork, meterFactory);
        await using var serviceScope = provider.CreateAsyncScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        using var request = TestSource.StartActivity("request")!;
        var scope = manager.BeginScope(isTransactional);
        await scope.CommitAsync(TestContext.Current.CancellationToken);

        var duration = collector.MeasurementsOf("eksen.unit_of_work.duration").ShouldHaveSingleItem();
        duration.Tag("eksen.uow.outcome").ShouldBe("commit");
        duration.Tag("eksen.uow.transactional").ShouldBe(isTransactional);
        request.Events.ShouldContain(activityEvent => activityEvent.Name == "uow.commit");
    }

    [Fact]
    public async Task Rollback_Records_The_Rollback_Outcome()
    {
        var (provider, meterFactory) = CreateProvider();
        await using var _ = provider;
        using var collector = new TelemetryCollector(EksenTelemetry.UnitOfWork, meterFactory);
        await using var serviceScope = provider.CreateAsyncScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        using var request = TestSource.StartActivity("request")!;
        var scope = manager.BeginScope();
        await scope.RollbackAsync(TestContext.Current.CancellationToken);

        collector.MeasurementsOf("eksen.unit_of_work.duration").ShouldHaveSingleItem()
            .Tag("eksen.uow.outcome").ShouldBe("rollback");
        request.Events.ShouldContain(activityEvent => activityEvent.Name == "uow.rollback");
    }

    [Fact]
    public async Task Disposal_Without_Commit_Records_Disposed_Uncommitted_Once()
    {
        var (provider, meterFactory) = CreateProvider();
        await using var _ = provider;
        using var collector = new TelemetryCollector(EksenTelemetry.UnitOfWork, meterFactory);
        await using var serviceScope = provider.CreateAsyncScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        var scope = manager.BeginScope();
        await scope.DisposeAsync();
        await scope.DisposeAsync();

        collector.MeasurementsOf("eksen.unit_of_work.duration").ShouldHaveSingleItem()
            .Tag("eksen.uow.outcome").ShouldBe("disposed_uncommitted");
    }

    [Fact]
    public async Task Failed_Provider_Commit_Is_Recorded_As_A_Rollback_With_The_Error_Type()
    {
        var (provider, meterFactory) = CreateProvider();
        await using var _ = provider;
        using var collector = new TelemetryCollector(EksenTelemetry.UnitOfWork, meterFactory);
        await using var serviceScope = provider.CreateAsyncScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var failing = new Mock<IUnitOfWorkProviderScope>();
        failing.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("db"));
        failing.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var scope = manager.BeginScope();
        scope.AddProviderScope(failing.Object);
        await Should.ThrowAsync<TimeoutException>(() => scope.CommitAsync(TestContext.Current.CancellationToken));

        var duration = collector.MeasurementsOf("eksen.unit_of_work.duration").ShouldHaveSingleItem();
        duration.Tag("eksen.uow.outcome").ShouldBe("rollback");
        duration.Tag("error.type").ShouldBe(typeof(TimeoutException).FullName);
    }

    [Fact]
    public async Task Post_Commit_Action_Failure_Is_Counted_And_Still_Thrown()
    {
        var (provider, meterFactory) = CreateProvider();
        await using var _ = provider;
        using var collector = new TelemetryCollector(EksenTelemetry.UnitOfWork, meterFactory);
        await using var serviceScope = provider.CreateAsyncScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        var scope = manager.BeginScope();
        scope.AddPostCommitAction((_, _) => throw new InvalidOperationException("notify failed"));
        await Should.ThrowAsync<InvalidOperationException>(() => scope.CommitAsync(TestContext.Current.CancellationToken));

        collector.MeasurementsOf("eksen.unit_of_work.post_commit_failures").ShouldHaveSingleItem()
            .Tag("error.type").ShouldBe(typeof(InvalidOperationException).FullName);
        collector.MeasurementsOf("eksen.unit_of_work.duration").ShouldHaveSingleItem()
            .Tag("eksen.uow.outcome").ShouldBe("commit");
    }

    [Fact]
    public async Task Uow_Span_Is_Off_By_Default()
    {
        var (provider, meterFactory) = CreateProvider();
        await using var _ = provider;
        using var collector = new TelemetryCollector(EksenTelemetry.UnitOfWork, meterFactory);
        await using var serviceScope = provider.CreateAsyncScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        using var request = TestSource.StartActivity("request")!;
        await manager.BeginScope().CommitAsync(TestContext.Current.CancellationToken);

        collector.ActivitiesIn(request.TraceId).ShouldNotContain(activity => activity.DisplayName == "uow");
    }

    [Fact]
    public async Task Uow_Span_Is_Recorded_When_Enabled_Without_Becoming_Current()
    {
        var (provider, meterFactory) = CreateProvider(options => options.UnitOfWorkSpans = true);
        await using var _ = provider;
        using var collector = new TelemetryCollector(EksenTelemetry.UnitOfWork, meterFactory);
        await using var serviceScope = provider.CreateAsyncScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        using var request = TestSource.StartActivity("request")!;
        var scope = manager.BeginScope(isTransactional: false);
        Activity.Current.ShouldBeSameAs(request);
        await scope.CommitAsync(TestContext.Current.CancellationToken);
        Activity.Current.ShouldBeSameAs(request);

        var uow = collector.ActivitiesIn(request.TraceId).Single(activity => activity.DisplayName == "uow");
        uow.ParentSpanId.ShouldBe(request.SpanId);
        uow.GetTagItem("eksen.uow.outcome").ShouldBe("commit");
        uow.GetTagItem("eksen.uow.transactional").ShouldBe(false);
        uow.Events.ShouldContain(activityEvent => activityEvent.Name == "uow.commit");
    }

    [Fact]
    public async Task AddUnitOfWork_Registers_The_Source_And_Meter()
    {
        var (provider, _) = CreateProvider();
        await using var _ = provider;

        Eksen.Telemetry.Registry.EksenTelemetryRegistry.GetSourceNames(provider).ShouldContain(EksenTelemetry.UnitOfWork);
        Eksen.Telemetry.Registry.EksenTelemetryRegistry.GetMeterNames(provider).ShouldContain(EksenTelemetry.UnitOfWork);
    }
}
