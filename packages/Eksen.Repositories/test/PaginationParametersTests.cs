using Eksen.Repositories;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Repositories.Tests;

public class BasePaginationParametersTests : EksenUnitTestBase
{
    [Fact]
    public void SkipCount_Should_Default_To_Null()
    {
        // Arrange & Act
        var pagination = new BasePaginationParameters();

        // Assert
        pagination.SkipCount.ShouldBeNull();
    }

    [Fact]
    public void MaxResultCount_Should_Default_To_Null()
    {
        // Arrange & Act
        var pagination = new BasePaginationParameters();

        // Assert
        pagination.MaxResultCount.ShouldBeNull();
    }

    [Fact]
    public void SkipCount_Should_Be_Settable()
    {
        // Arrange & Act
        var pagination = new BasePaginationParameters { SkipCount = 10 };

        // Assert
        pagination.SkipCount.ShouldBe(10);
    }

    [Fact]
    public void MaxResultCount_Should_Be_Settable()
    {
        // Arrange & Act
        var pagination = new BasePaginationParameters { MaxResultCount = 20 };

        // Assert
        pagination.MaxResultCount.ShouldBe(20);
    }
}

public class DefaultPaginationParametersTests : EksenUnitTestBase
{
    [Fact]
    public void Should_Inherit_BasePaginationParameters()
    {
        // Arrange & Act
        var pagination = new DefaultPaginationParameters();

        // Assert
        pagination.ShouldBeAssignableTo<BasePaginationParameters>();
    }
}
