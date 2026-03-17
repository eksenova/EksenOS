using Eksen.TestBase;
using Shouldly;

namespace Eksen.ErrorHandling.Tests;

public class ErrorInstanceTests : EksenUnitTestBase
{
    private static readonly ErrorDescriptor TestError = new(
        ErrorType.Validation, "TestModule");

    [Fact]
    public void Constructor_Should_Set_Descriptor()
    {
        // Arrange & Act
        var instance = new ErrorInstance(TestError);

        // Assert
        instance.Descriptor.ShouldBe(TestError);
    }

    [Fact]
    public void Data_Should_Be_Empty_By_Default()
    {
        // Arrange & Act
        var instance = new ErrorInstance(TestError);

        // Assert
        instance.Data.ShouldBeEmpty();
    }

    [Fact]
    public void WithData_Should_Add_Key_Value_Pair()
    {
        // Arrange
        var instance = new ErrorInstance(TestError);

        // Act
        var result = instance.WithData("key", "value");

        // Assert
        result.Data.ShouldContainKeyAndValue("key", "value");
        result.Descriptor.ShouldBe(TestError);
    }

    [Fact]
    public void WithData_Should_Not_Mutate_Original_Instance()
    {
        // Arrange
        var instance = new ErrorInstance(TestError);

        // Act
        instance.WithData("key", "value");

        // Assert
        instance.Data.ShouldBeEmpty();
    }

    [Fact]
    public void WithData_Dictionary_Should_Merge_Data()
    {
        // Arrange
        var instance = new ErrorInstance(TestError).WithData("existing", "val");
        var newData = new Dictionary<string, object?> { ["existing"] = "newVal" };

        // Act
        var result = instance.WithData(newData);

        // Assert
        result.Data["existing"].ShouldBe("newVal");
    }

    [Fact]
    public void WithData_Should_Throw_When_Key_Is_Null()
    {
        // Arrange
        var instance = new ErrorInstance(TestError);

        // Act & Assert
        Should.Throw<ArgumentNullException>(
            () => instance.WithData(null!, "value"));
    }

    [Fact]
    public void WithValue_Should_Add_Value_With_Expression_Name()
    {
        // Arrange
        var instance = new ErrorInstance(TestError);
        var orderId = 42;

        // Act
        var result = instance.WithValue(orderId);

        // Assert
        result.Data.ShouldContainKey("orderId");
        result.Data["orderId"].ShouldBe("42");
    }

    [Fact]
    public void WithValue_Should_Convert_Type_To_Name()
    {
        // Arrange
        var instance = new ErrorInstance(TestError);
        var type = typeof(string);

        // Act
        var result = instance.WithValue(type);

        // Assert
        result.Data["type"].ShouldBe("String");
    }

    [Fact]
    public void Implicit_Conversion_To_Exception_Should_Return_EksenException()
    {
        // Arrange
        var instance = new ErrorInstance(TestError).WithData("key", "value");

        // Act
        Exception exception = instance;

        // Assert
        exception.ShouldBeOfType<EksenException>();
        var eksenException = (EksenException)exception;
        eksenException.Descriptor.ShouldBe(TestError);
    }
}
