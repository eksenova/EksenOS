using Eksen.TestBase;
using Shouldly;

namespace Eksen.ErrorHandling.Tests;

public class BaseErrorDescriptorTests : EksenUnitTestBase
{
    [Fact]
    public void Code_Should_Be_Category_Dot_MemberName()
    {
        // Arrange & Act
        var descriptor = new ErrorDescriptor(ErrorType.Validation, "Orders");

        // Assert
        descriptor.Code.ShouldBe("Orders.Code_Should_Be_Category_Dot_MemberName");
        descriptor.ErrorType.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void Constructor_Should_Throw_When_Category_Is_Null()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(
            () => new ErrorDescriptor(ErrorType.Validation, null!));
    }

    [Fact]
    public void Constructor_Should_Throw_When_Category_Is_Empty()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(
            () => new ErrorDescriptor(ErrorType.Validation, ""));
    }

    [Fact]
    public void Constructor_Should_Throw_When_ErrorType_Is_Null()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(
            () => new ErrorDescriptor(null!, "Orders"));
    }

    [Fact]
    public void Constructor_Should_Throw_When_ErrorType_Is_Empty()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(
            () => new ErrorDescriptor("", "Orders"));
    }
}
