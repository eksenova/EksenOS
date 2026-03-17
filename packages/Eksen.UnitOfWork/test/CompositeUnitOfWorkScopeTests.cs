using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;

namespace Eksen.UnitOfWork.Tests;

public class CompositeUnitOfWorkScopeTests : EksenUnitTestBase
{
    private static UnitOfWorkManager CreateManager()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEnumerable<IUnitOfWorkProvider>>(
            Array.Empty<IUnitOfWorkProvider>());
        var sp = services.BuildServiceProvider();
        return new UnitOfWorkManager(sp);
    }

    [Fact]
    public void ScopeId_Should_Be_Unique()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        var scope1 = manager.BeginScope();
        var scope2 = manager.BeginScope();

        // Assert
        scope1.ScopeId.ShouldNotBe(scope2.ScopeId);
    }

    [Fact]
    public void AddProviderScope_Should_Add_To_ProviderScopes()
    {
        // Arrange
        var manager = CreateManager();
        var scope = manager.BeginScope();
        var providerScope = new Mock<IUnitOfWorkProviderScope>();

        // Act
        scope.AddProviderScope(providerScope.Object);

        // Assert
        scope.ProviderScopes.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CommitAsync_Should_Call_CommitAsync_On_All_Provider_Scopes()
    {
        // Arrange
        var providerScope1 = new Mock<IUnitOfWorkProviderScope>();
        providerScope1.Setup(s => s.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        providerScope1.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var providerScope2 = new Mock<IUnitOfWorkProviderScope>();
        providerScope2.Setup(s => s.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        providerScope2.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var provider1 = new Mock<IUnitOfWorkProvider>();
        provider1
            .Setup(p => p.BeginScope(
                It.IsAny<IUnitOfWorkScope>(),
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(providerScope1.Object);

        var provider2 = new Mock<IUnitOfWorkProvider>();
        provider2
            .Setup(p => p.BeginScope(
                It.IsAny<IUnitOfWorkScope>(),
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(providerScope2.Object);

        var services = new ServiceCollection();
        services.AddSingleton<IEnumerable<IUnitOfWorkProvider>>(
            new[] { provider1.Object, provider2.Object });
        var sp = services.BuildServiceProvider();
        var manager = new UnitOfWorkManager(sp);

        var scope = manager.BeginScope();

        // Act
        await scope.CommitAsync();

        // Assert
        providerScope1.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        providerScope2.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RollbackAsync_Should_Call_RollbackAsync_On_All_Provider_Scopes()
    {
        // Arrange
        var providerScope = new Mock<IUnitOfWorkProviderScope>();
        providerScope.Setup(s => s.RollbackAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        providerScope.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var provider = new Mock<IUnitOfWorkProvider>();
        provider
            .Setup(p => p.BeginScope(
                It.IsAny<IUnitOfWorkScope>(),
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(providerScope.Object);

        var services = new ServiceCollection();
        services.AddSingleton<IEnumerable<IUnitOfWorkProvider>>(
            new[] { provider.Object });
        var sp = services.BuildServiceProvider();
        var manager = new UnitOfWorkManager(sp);

        var scope = manager.BeginScope();

        // Act
        await scope.RollbackAsync();

        // Assert
        providerScope.Verify(s => s.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveChangesAsync_Should_Call_SaveChangesAsync_On_All_Provider_Scopes()
    {
        // Arrange
        var providerScope = new Mock<IUnitOfWorkProviderScope>();
        providerScope.Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var provider = new Mock<IUnitOfWorkProvider>();
        provider
            .Setup(p => p.BeginScope(
                It.IsAny<IUnitOfWorkScope>(),
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(providerScope.Object);

        var services = new ServiceCollection();
        services.AddSingleton<IEnumerable<IUnitOfWorkProvider>>(
            new[] { provider.Object });
        var sp = services.BuildServiceProvider();
        var manager = new UnitOfWorkManager(sp);

        var scope = manager.BeginScope();

        // Act
        await scope.SaveChangesAsync();

        // Assert
        providerScope.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitAsync_Should_Invoke_Completing_Callbacks_Before_Commit()
    {
        // Arrange
        var manager = CreateManager();
        var scope = manager.BeginScope();

        var callOrder = new List<string>();

        scope.AddCompletingCallback((_, _) =>
        {
            callOrder.Add("completing");
            return Task.CompletedTask;
        });

        // Act
        await scope.CommitAsync();

        // Assert
        callOrder.ShouldContain("completing");
    }

    [Fact]
    public async Task CommitAsync_Should_Invoke_Completed_Callbacks_After_Commit()
    {
        // Arrange
        var manager = CreateManager();
        var scope = manager.BeginScope();

        var completedCalled = false;

        scope.AddCompletedCallback((_, _) =>
        {
            completedCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await scope.CommitAsync();

        // Assert
        completedCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task CommitAsync_Should_Invoke_PostCommitActions_After_Completed_Callbacks()
    {
        // Arrange
        var manager = CreateManager();
        var scope = manager.BeginScope();

        var callOrder = new List<string>();

        scope.AddCompletedCallback((_, _) =>
        {
            callOrder.Add("completed");
            return Task.CompletedTask;
        });

        scope.AddPostCommitAction((_, _) =>
        {
            callOrder.Add("postcommit");
            return Task.CompletedTask;
        });

        // Act
        await scope.CommitAsync();

        // Assert
        callOrder.Count.ShouldBe(2);
        callOrder[0].ShouldBe("completed");
        callOrder[1].ShouldBe("postcommit");
    }

    [Fact]
    public async Task RollbackAsync_Should_Invoke_Rollback_Callbacks()
    {
        // Arrange
        var manager = CreateManager();
        var scope = manager.BeginScope();

        var rollbackCalled = false;

        scope.AddRollbackCallback((_, _) =>
        {
            rollbackCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await scope.RollbackAsync();

        // Assert
        rollbackCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposeAsync_Should_Dispose_All_Provider_Scopes()
    {
        // Arrange
        var providerScope = new Mock<IUnitOfWorkProviderScope>();
        providerScope.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var provider = new Mock<IUnitOfWorkProvider>();
        provider
            .Setup(p => p.BeginScope(
                It.IsAny<IUnitOfWorkScope>(),
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(providerScope.Object);

        var services = new ServiceCollection();
        services.AddSingleton<IEnumerable<IUnitOfWorkProvider>>(
            new[] { provider.Object });
        var sp = services.BuildServiceProvider();
        var manager = new UnitOfWorkManager(sp);

        var scope = manager.BeginScope();

        // Act
        await scope.DisposeAsync();

        // Assert
        providerScope.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_Should_Be_Idempotent()
    {
        // Arrange
        var manager = CreateManager();
        var scope = manager.BeginScope();

        // Act
        await scope.DisposeAsync();
        await scope.DisposeAsync(); // second call should not throw

        // Assert
        manager.Current.ShouldBeNull();
    }

    [Fact]
    public async Task CommitAsync_Should_Invoke_Completing_Before_Completed()
    {
        // Arrange
        var manager = CreateManager();
        var scope = manager.BeginScope();

        var callOrder = new List<string>();

        scope.AddCompletingCallback((_, _) =>
        {
            callOrder.Add("completing");
            return Task.CompletedTask;
        });

        scope.AddCompletedCallback((_, _) =>
        {
            callOrder.Add("completed");
            return Task.CompletedTask;
        });

        // Act
        await scope.CommitAsync();

        // Assert
        callOrder.Count.ShouldBe(2);
        callOrder[0].ShouldBe("completing");
        callOrder[1].ShouldBe("completed");
    }

    [Fact]
    public async Task CommitAsync_Should_Dispose_Scope_After_Commit()
    {
        // Arrange
        var manager = CreateManager();
        var scope = manager.BeginScope();

        // Act
        await scope.CommitAsync();

        // Assert
        manager.Current.ShouldBeNull();
    }

    [Fact]
    public async Task RollbackAsync_Should_Dispose_Scope_After_Rollback()
    {
        // Arrange
        var manager = CreateManager();
        var scope = manager.BeginScope();

        // Act
        await scope.RollbackAsync();

        // Assert
        manager.Current.ShouldBeNull();
    }
}
