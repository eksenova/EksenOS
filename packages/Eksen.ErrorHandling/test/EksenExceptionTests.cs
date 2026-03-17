using Eksen.TestBase;
using Shouldly;

namespace Eksen.ErrorHandling.Tests;

public class EksenExceptionTests : EksenUnitTestBase
{
    private static readonly ErrorDescriptor TestError = new(
        ErrorType.Validation, "TestModule");

    [Fact]
    public void Constructor_With_Descriptor_Should_Set_Properties()
    {
        // Arrange & Act
        var exception = new EksenException(TestError);

        // Assert
        exception.Descriptor.ShouldBe(TestError);
        exception.Message.ShouldBe(TestError.Code);
    }

    [Fact]
    public void Constructor_With_ErrorInstance_Should_Copy_Data()
    {
        // Arrange
        var instance = new ErrorInstance(TestError)
            .WithData("key1", "value1")
            .WithData("key2", 42);

        // Act
        var exception = new EksenException(instance);

        // Assert
        exception.Descriptor.ShouldBe(TestError);
        exception.Data["key1"].ShouldBe("value1");
        exception.Data["key2"].ShouldBe(42);
    }

    [Fact]
    public void Message_Should_Return_Descriptor_Code()
    {
        // Arrange
        var exception = new EksenException(TestError);

        // Assert
        exception.Message.ShouldBe("TestModule.TestError");
    }

    [Fact]
    public void IErrorData_Data_Should_Return_Dictionary()
    {
        // Arrange
        var instance = new ErrorInstance(TestError).WithData("key", "value");
        var exception = new EksenException(instance);

        // Act
        var errorData = (IErrorData)exception;
        var data = errorData.Data;

        // Assert
        data.ShouldContainKeyAndValue("key", "value");
    }
}
