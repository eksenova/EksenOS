using Eksen.TestBase;
using Shouldly;

namespace Eksen.ErrorHandling.Tests;

public class ErrorTypeTests : EksenUnitTestBase
{
    [Fact]
    public void NotFound_Should_Return_Correct_Value()
    {
        // Arrange & Act & Assert
        ErrorType.NotFound.ShouldBe("NotFound");
    }

    [Fact]
    public void Authorization_Should_Return_Correct_Value()
    {
        // Arrange & Act & Assert
        ErrorType.Authorization.ShouldBe("Authorization");
    }

    [Fact]
    public void Validation_Should_Return_Correct_Value()
    {
        // Arrange & Act & Assert
        ErrorType.Validation.ShouldBe("Validation");
    }

    [Fact]
    public void Conflict_Should_Return_Correct_Value()
    {
        // Arrange & Act & Assert
        ErrorType.Conflict.ShouldBe("Conflict");
    }

    [Fact]
    public void RateLimit_Should_Return_Correct_Value()
    {
        // Arrange & Act & Assert
        ErrorType.RateLimit.ShouldBe("RateLimit");
    }
}
