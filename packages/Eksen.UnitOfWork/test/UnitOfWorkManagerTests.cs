using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;

namespace Eksen.UnitOfWork.Tests;

public class UnitOfWorkManagerTests : EksenUnitTestBase
{
    [Fact]
    public void BeginScope_Should_Return_UnitOfWorkScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IEnumerable<IUnitOfWorkProvider>>(
            Array.Empty<IUnitOfWorkProvider>());
        var sp = services.BuildServiceProvider();
        var manager = new UnitOfWorkManager(sp);

        // Act
        var scope = manager.BeginScope();

        // Assert
        scope.ShouldNotBeNull();
        scope.ScopeId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Current_Should_Return_Null_When_No_Scope_Is_Active()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IEnumerable<IUnitOfWorkProvider>>(
            Array.Empty<IUnitOfWorkProvider>());
        var sp = services.BuildServiceProvider();
        var manager = new UnitOfWorkManager(sp);

        // Act
        var current = manager.Current;

        // Assert
        current.ShouldBeNull();
    }

    [Fact]
    public void Current_Should_Return_Most_Recent_Scope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IEnumerable<IUnitOfWorkProvider>>(
            Array.Empty<IUnitOfWorkProvider>());
        var sp = services.BuildServiceProvider();
        var manager = new UnitOfWorkManager(sp);

        // Act
        var scope = manager.BeginScope();

        // Assert
        manager.Current.ShouldBe(scope);
    }

    [Fact]
    public void BeginScope_Should_Call_Provider_BeginScope_For_Each_Provider()
    {
        // Arrange
        var providerScope = new Mock<IUnitOfWorkProviderScope>();

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

        // Act
        var scope = manager.BeginScope(isTransactional: true);

        // Assert
        provider.Verify(
            p => p.BeginScope(
                It.IsAny<IUnitOfWorkScope>(),
                true,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);

        scope.ProviderScopes.Count.ShouldBe(1);
    }

    [Fact]
    public void BeginScope_Should_Pass_IsolationLevel_To_Providers()
    {
        // Arrange
        var providerScope = new Mock<IUnitOfWorkProviderScope>();

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

        // Act
        manager.BeginScope(
            isTransactional: true,
            isolationLevel: System.Data.IsolationLevel.Serializable);

        // Assert
        provider.Verify(
            p => p.BeginScope(
                It.IsAny<IUnitOfWorkScope>(),
                true,
                System.Data.IsolationLevel.Serializable,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PopScope_Should_Remove_Current_Scope_After_Dispose()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IEnumerable<IUnitOfWorkProvider>>(
            Array.Empty<IUnitOfWorkProvider>());
        var sp = services.BuildServiceProvider();
        var manager = new UnitOfWorkManager(sp);

        var scope = manager.BeginScope();
        manager.Current.ShouldBe(scope);

        // Act
        await scope.DisposeAsync();

        // Assert
        manager.Current.ShouldBeNull();
    }

    [Fact]
    public async Task Nested_Scopes_Should_Pop_In_Correct_Order()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IEnumerable<IUnitOfWorkProvider>>(
            Array.Empty<IUnitOfWorkProvider>());
        var sp = services.BuildServiceProvider();
        var manager = new UnitOfWorkManager(sp);

        var scope1 = manager.BeginScope();
        var scope2 = manager.BeginScope();

        // Act & Assert
        manager.Current.ShouldBe(scope2);
        await scope2.DisposeAsync();
        manager.Current.ShouldBe(scope1);
        await scope1.DisposeAsync();
        manager.Current.ShouldBeNull();
    }

    [Fact]
    public void BeginScope_Should_Register_Multiple_Provider_Scopes()
    {
        // Arrange
        var providerScope1 = new Mock<IUnitOfWorkProviderScope>();
        var provider1 = new Mock<IUnitOfWorkProvider>();
        provider1
            .Setup(p => p.BeginScope(
                It.IsAny<IUnitOfWorkScope>(),
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(providerScope1.Object);

        var providerScope2 = new Mock<IUnitOfWorkProviderScope>();
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

        // Act
        var scope = manager.BeginScope();

        // Assert
        scope.ProviderScopes.Count.ShouldBe(2);
    }
}
