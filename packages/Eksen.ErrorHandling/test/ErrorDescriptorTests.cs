using Eksen.TestBase;
using Shouldly;

namespace Eksen.ErrorHandling.Tests;

public class ErrorDescriptorTests : EksenUnitTestBase
{
    private static readonly ErrorDescriptor TestError = new(
        ErrorType.Validation, "TestModule");

    [Fact]
    public void Raise_Should_Return_ErrorInstance_With_Correct_Descriptor()
    {
        // Arrange & Act
        var instance = TestError.Raise();

        // Assert
        instance.ShouldNotBeNull();
        instance.Descriptor.ShouldBe(TestError);
        instance.Data.ShouldBeEmpty();
    }

    [Fact]
    public void Code_Should_Follow_Category_Dot_Code_Format()
    {
        // Arrange & Act & Assert
        TestError.Code.ShouldBe("TestModule.TestError");
    }

    [Fact]
    public void ErrorType_Should_Be_Set_Correctly()
    {
        // Arrange & Act & Assert
        TestError.ErrorType.ShouldBe(ErrorType.Validation);
    }
}

public class ErrorDescriptorWithDelegateTests : EksenUnitTestBase
{
    public delegate ErrorInstance TestRaiseDelegate(string param);

    private static readonly ErrorDescriptor<TestRaiseDelegate> TestError = new(
        ErrorType.NotFound,
        "TestModule",
        self => param => new ErrorInstance(self).WithData("param", param));

    [Fact]
    public void Raise_Should_Create_ErrorInstance_With_Data()
    {
        // Arrange & Act
        var instance = TestError.Raise("testValue");

        // Assert
        instance.ShouldNotBeNull();
        instance.Descriptor.ShouldBe(TestError);
        instance.Data.ShouldContainKeyAndValue("param", "testValue");
    }

    [Fact]
    public void Constructor_Should_Throw_When_RaiseFunc_Is_Null()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(
            () => new ErrorDescriptor<TestRaiseDelegate>(
                ErrorType.NotFound,
                "TestModule",
                null!));
    }

    [Fact]
    public void Constructor_Should_Throw_When_Delegate_Returns_Non_ErrorInstance()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(
            () => new ErrorDescriptor<Func<string>>(
                ErrorType.NotFound,
                "TestModule",
                _ => () => "not an ErrorInstance"));
    }
}
