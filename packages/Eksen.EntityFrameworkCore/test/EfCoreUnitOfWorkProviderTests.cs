using Eksen.TestBase;
using Eksen.UnitOfWork;
using Moq;
using Shouldly;

namespace Eksen.EntityFrameworkCore.Tests;

public class EfCoreUnitOfWorkProviderTests : EksenUnitTestBase
{
    [Fact]
    public void BeginScope_Should_Return_EfCoreUnitOfWorkScope()
    {
        // Arrange
        var tracker = new DbContextTracker();
        var provider = new EfCoreUnitOfWorkProvider(tracker);
        var parentScope = new Mock<IUnitOfWorkScope>().Object;

        // Act
        var scope = provider.BeginScope(parentScope, isTransctional: true);

        // Assert
        scope.ShouldNotBeNull();
        scope.ShouldBeOfType<EfCoreUnitOfWorkScope>();
        scope.Provider.ShouldBe(provider);
        scope.ParentScope.ShouldBe(parentScope);
    }

    [Fact]
    public void BeginScope_Should_Accept_Non_Transactional()
    {
        // Arrange
        var tracker = new DbContextTracker();
        var provider = new EfCoreUnitOfWorkProvider(tracker);
        var parentScope = new Mock<IUnitOfWorkScope>().Object;

        // Act
        var scope = provider.BeginScope(parentScope, isTransctional: false);

        // Assert
        scope.ShouldNotBeNull();
        scope.ShouldBeOfType<EfCoreUnitOfWorkScope>();
    }
}
