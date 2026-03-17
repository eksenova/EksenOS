using Eksen.TestBase;
using Eksen.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Eksen.DataSeeding.Tests;

public class DataSeederTests : EksenUnitTestBase
{
    private static (DataSeeder seeder, Mock<IUnitOfWorkScope> scope) CreateSeeder(
        params Type[] contributorTypes)
    {
        var services = new ServiceCollection();

        foreach (var type in contributorTypes)
        {
            services.AddTransient(type);
        }

        var serviceProvider = services.BuildServiceProvider();

        var scope = new Mock<IUnitOfWorkScope>();
        scope.Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        scope.Setup(s => s.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        var unitOfWorkManager = new Mock<IUnitOfWorkManager>();
        unitOfWorkManager
            .Setup(u => u.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(scope.Object);

        var options = Options.Create(new EksenDataSeedingOptions());
        options.Value.AddRange(contributorTypes);

        var seeder = new DataSeeder(serviceProvider, unitOfWorkManager.Object, options);
        return (seeder, scope);
    }

    [Fact]
    public async Task SeedAsync_Should_Execute_All_Contributors()
    {
        // Arrange
        var (seeder, scope) = CreateSeeder(typeof(StubContributorA), typeof(StubContributorB));

        // Act
        await seeder.SeedAsync();

        // Assert
        scope.Verify(
            s => s.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SeedAsync_Should_Do_Nothing_When_No_Contributors()
    {
        // Arrange
        var (seeder, scope) = CreateSeeder();

        // Act
        await seeder.SeedAsync();

        // Assert
        scope.Verify(
            s => s.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SeedAsync_Should_Execute_Dependencies_Before_Dependents()
    {
        // Arrange
        var executionOrder = new List<string>();

        var services = new ServiceCollection();
        services.AddSingleton(executionOrder);
        services.AddTransient<OrderTrackingContributorA>();
        services.AddTransient<OrderTrackingContributorAfterA>();

        var serviceProvider = services.BuildServiceProvider();

        var scope = new Mock<IUnitOfWorkScope>();
        scope.Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        scope.Setup(s => s.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        var unitOfWorkManager = new Mock<IUnitOfWorkManager>();
        unitOfWorkManager
            .Setup(u => u.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(scope.Object);

        var options = Options.Create(new EksenDataSeedingOptions());
        // Register dependent first to verify ordering
        options.Value.AddRange([typeof(OrderTrackingContributorAfterA), typeof(OrderTrackingContributorA)]);

        var seeder = new DataSeeder(serviceProvider, unitOfWorkManager.Object, options);

        // Act
        await seeder.SeedAsync();

        // Assert
        executionOrder.Count.ShouldBe(2);
        executionOrder[0].ShouldBe(nameof(OrderTrackingContributorA));
        executionOrder[1].ShouldBe(nameof(OrderTrackingContributorAfterA));
    }

    [Fact]
    public async Task SeedAsync_Should_Throw_When_SeedAfter_Target_Not_Registered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ContributorWithMissingSeedAfter>();

        var serviceProvider = services.BuildServiceProvider();

        var scope = new Mock<IUnitOfWorkScope>();
        scope.Setup(s => s.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        var unitOfWorkManager = new Mock<IUnitOfWorkManager>();
        unitOfWorkManager
            .Setup(u => u.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(scope.Object);

        var options = Options.Create(new EksenDataSeedingOptions());
        // Only register the dependent, not the dependency
        options.Value.Add(typeof(ContributorWithMissingSeedAfter));

        var seeder = new DataSeeder(serviceProvider, unitOfWorkManager.Object, options);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => seeder.SeedAsync());
    }

    [Fact]
    public async Task SeedAsync_Should_Dispose_Disposable_Contributors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<DisposableTracker>();
        services.AddTransient<TrackingDisposableContributor>();

        var serviceProvider = services.BuildServiceProvider();

        var scope = new Mock<IUnitOfWorkScope>();
        scope.Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        scope.Setup(s => s.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        var unitOfWorkManager = new Mock<IUnitOfWorkManager>();
        unitOfWorkManager
            .Setup(u => u.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(scope.Object);

        var options = Options.Create(new EksenDataSeedingOptions());
        options.Value.Add(typeof(TrackingDisposableContributor));

        var seeder = new DataSeeder(serviceProvider, unitOfWorkManager.Object, options);

        // Act
        await seeder.SeedAsync();

        // Assert
        var tracker = serviceProvider.GetRequiredService<DisposableTracker>();
        tracker.DisposeCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task SeedAsync_Should_DisposeAsync_AsyncDisposable_Contributors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<DisposableTracker>();
        services.AddTransient<TrackingAsyncDisposableContributor>();

        var serviceProvider = services.BuildServiceProvider();

        var scope = new Mock<IUnitOfWorkScope>();
        scope.Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        scope.Setup(s => s.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        var unitOfWorkManager = new Mock<IUnitOfWorkManager>();
        unitOfWorkManager
            .Setup(u => u.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(scope.Object);

        var options = Options.Create(new EksenDataSeedingOptions());
        options.Value.Add(typeof(TrackingAsyncDisposableContributor));

        var seeder = new DataSeeder(serviceProvider, unitOfWorkManager.Object, options);

        // Act
        await seeder.SeedAsync();

        // Assert
        var tracker = serviceProvider.GetRequiredService<DisposableTracker>();
        tracker.AsyncDisposeCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task SeedAsync_Should_Begin_Transactional_Scope()
    {
        // Arrange
        var scope = new Mock<IUnitOfWorkScope>();
        scope.Setup(s => s.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        var unitOfWorkManager = new Mock<IUnitOfWorkManager>();
        unitOfWorkManager
            .Setup(u => u.BeginScope(
                true,
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(scope.Object);

        var options = Options.Create(new EksenDataSeedingOptions());

        var seeder = new DataSeeder(
            new ServiceCollection().BuildServiceProvider(),
            unitOfWorkManager.Object,
            options);

        // Act
        await seeder.SeedAsync();

        // Assert
        unitOfWorkManager.Verify(
            u => u.BeginScope(true, It.IsAny<System.Data.IsolationLevel?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SeedAsync_Should_SaveChanges_After_Each_Contributor()
    {
        // Arrange
        var saveOrder = new List<string>();

        var services = new ServiceCollection();
        services.AddSingleton(saveOrder);
        services.AddTransient<OrderTrackingContributorA>();
        services.AddTransient<OrderTrackingContributorB>();

        var serviceProvider = services.BuildServiceProvider();

        var scope = new Mock<IUnitOfWorkScope>();
        scope.Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => saveOrder.Add("SaveChanges"))
            .Returns(Task.CompletedTask);
        scope.Setup(s => s.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        var unitOfWorkManager = new Mock<IUnitOfWorkManager>();
        unitOfWorkManager
            .Setup(u => u.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(scope.Object);

        var options = Options.Create(new EksenDataSeedingOptions());
        options.Value.AddRange([typeof(OrderTrackingContributorA), typeof(OrderTrackingContributorB)]);

        var seeder = new DataSeeder(serviceProvider, unitOfWorkManager.Object, options);

        // Act
        await seeder.SeedAsync();

        // Assert
        // Expected: A seeds, SaveChanges, B seeds, SaveChanges
        saveOrder.Count.ShouldBe(4);
        saveOrder[0].ShouldBe(nameof(OrderTrackingContributorA));
        saveOrder[1].ShouldBe("SaveChanges");
        saveOrder[2].ShouldBe(nameof(OrderTrackingContributorB));
        saveOrder[3].ShouldBe("SaveChanges");
    }

    [Fact]
    public async Task SeedAsync_Should_Not_Duplicate_Contributors()
    {
        // Arrange
        var executionOrder = new List<string>();

        var services = new ServiceCollection();
        services.AddSingleton(executionOrder);
        services.AddTransient<OrderTrackingContributorA>();
        services.AddTransient<OrderTrackingContributorAfterA>();

        var serviceProvider = services.BuildServiceProvider();

        var scope = new Mock<IUnitOfWorkScope>();
        scope.Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        scope.Setup(s => s.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        var unitOfWorkManager = new Mock<IUnitOfWorkManager>();
        unitOfWorkManager
            .Setup(u => u.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(scope.Object);

        var options = Options.Create(new EksenDataSeedingOptions());
        // A is both registered directly and as a dependency of AfterA
        options.Value.AddRange([typeof(OrderTrackingContributorA), typeof(OrderTrackingContributorAfterA)]);

        var seeder = new DataSeeder(serviceProvider, unitOfWorkManager.Object, options);

        // Act
        await seeder.SeedAsync();

        // Assert
        executionOrder.Count(x => x == nameof(OrderTrackingContributorA)).ShouldBe(1);
    }
}

public class OrderTrackingContributorA(List<string> executionOrder) : IDataSeedContributor
{
    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        executionOrder.Add(nameof(OrderTrackingContributorA));
        return Task.CompletedTask;
    }
}

public class OrderTrackingContributorB(List<string> executionOrder) : IDataSeedContributor
{
    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        executionOrder.Add(nameof(OrderTrackingContributorB));
        return Task.CompletedTask;
    }
}

[SeedAfter(typeof(OrderTrackingContributorA))]
public class OrderTrackingContributorAfterA(List<string> executionOrder) : IDataSeedContributor
{
    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        executionOrder.Add(nameof(OrderTrackingContributorAfterA));
        return Task.CompletedTask;
    }
}
