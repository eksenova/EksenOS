using Eksen.Repositories;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Repositories.Tests;

public class BaseQueryOptionsTests : EksenUnitTestBase
{
    [Fact]
    public void IgnoreQueryFilters_Should_Default_To_False()
    {
        // Arrange & Act
        var options = new BaseQueryOptions();

        // Assert
        options.IgnoreQueryFilters.ShouldBeFalse();
    }

    [Fact]
    public void AsNoTracking_Should_Default_To_False()
    {
        // Arrange & Act
        var options = new BaseQueryOptions();

        // Assert
        options.AsNoTracking.ShouldBeFalse();
    }

    [Fact]
    public void IgnoreQueryFilters_Should_Be_Settable()
    {
        // Arrange & Act
        var options = new BaseQueryOptions { IgnoreQueryFilters = true };

        // Assert
        options.IgnoreQueryFilters.ShouldBeTrue();
    }

    [Fact]
    public void AsNoTracking_Should_Be_Settable()
    {
        // Arrange & Act
        var options = new BaseQueryOptions { AsNoTracking = true };

        // Assert
        options.AsNoTracking.ShouldBeTrue();
    }
}

public class DefaultQueryOptionsTests : EksenUnitTestBase
{
    [Fact]
    public void Should_Inherit_BaseQueryOptions()
    {
        // Arrange & Act
        var options = new DefaultQueryOptions();

        // Assert
        options.ShouldBeAssignableTo<BaseQueryOptions>();
    }
}
