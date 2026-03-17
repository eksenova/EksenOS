using Eksen.Repositories;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Repositories.Tests;

public class BaseSortingParametersTests : EksenUnitTestBase
{
    [Fact]
    public void Sorting_Should_Default_To_Null()
    {
        // Arrange & Act
        var sorting = new BaseSortingParameters<FakeEntity>();

        // Assert
        sorting.Sorting.ShouldBeNull();
    }

    [Fact]
    public void Sorting_Should_Be_Settable()
    {
        // Arrange & Act
        var sorting = new BaseSortingParameters<FakeEntity> { Sorting = "Name asc" };

        // Assert
        sorting.Sorting.ShouldBe("Name asc");
    }

    [Fact]
    public void Implicit_Conversion_Should_Create_From_String()
    {
        // Arrange & Act
        BaseSortingParameters<FakeEntity> sorting = "Name desc";

        // Assert
        sorting.Sorting.ShouldBe("Name desc");
    }
}

public class DefaultSortingParametersTests : EksenUnitTestBase
{
    [Fact]
    public void Should_Inherit_BaseSortingParameters()
    {
        // Arrange & Act
        var sorting = new DefaultSortingParameters<FakeEntity>();

        // Assert
        sorting.ShouldBeAssignableTo<BaseSortingParameters<FakeEntity>>();
    }
}
