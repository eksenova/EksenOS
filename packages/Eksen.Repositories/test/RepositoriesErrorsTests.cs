using Eksen.ErrorHandling;
using Eksen.Repositories;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Repositories.Tests;

public class RepositoriesErrorsTests : EksenUnitTestBase
{
    [Fact]
    public void NegativeSortingIndex_Should_Have_Category_In_ErrorType()
    {
        // Arrange & Act & Assert
        RepositoriesErrors.NegativeSortingIndex.ErrorType.ShouldBe(RepositoriesErrors.Category);
    }

    [Fact]
    public void InvalidSortingIndex_Should_Have_Category_In_ErrorType()
    {
        // Arrange & Act & Assert
        RepositoriesErrors.InvalidSortingIndex.ErrorType.ShouldBe(RepositoriesErrors.Category);
    }

    [Fact]
    public void NegativeSortingIndex_Should_Be_Validation_Error()
    {
        // Arrange & Act & Assert
        RepositoriesErrors.NegativeSortingIndex.Code.ShouldStartWith(ErrorType.Validation);
    }

    [Fact]
    public void InvalidSortingIndex_Should_Be_Validation_Error()
    {
        // Arrange & Act & Assert
        RepositoriesErrors.InvalidSortingIndex.Code.ShouldStartWith(ErrorType.Validation);
    }

    [Fact]
    public void NegativeSortingIndex_Should_Have_Correct_Code()
    {
        // Arrange & Act & Assert
        RepositoriesErrors.NegativeSortingIndex.Code.ShouldContain("NegativeSortingIndex");
    }

    [Fact]
    public void InvalidSortingIndex_Should_Have_Correct_Code()
    {
        // Arrange & Act & Assert
        RepositoriesErrors.InvalidSortingIndex.Code.ShouldContain("InvalidSortingIndex");
    }
}
