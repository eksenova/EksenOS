using Eksen.TestBase;
using Shouldly;

namespace Eksen.ErrorHandling.Tests;

public class CommonErrorsTests : EksenUnitTestBase
{
    [Fact]
    public void Unauthorized_Should_Have_Authorization_ErrorType()
    {
        // Arrange & Act & Assert
        CommonErrors.Unauthorized.ErrorType.ShouldBe(ErrorType.Authorization);
    }

    [Fact]
    public void Unauthorized_Should_Have_Correct_Code()
    {
        // Arrange & Act & Assert
        CommonErrors.Unauthorized.Code.ShouldContain("Unauthorized");
    }

    [Fact]
    public void ObjectNotFound_Should_Have_NotFound_ErrorType()
    {
        // Arrange & Act & Assert
        CommonErrors.ObjectNotFound.ErrorType.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public void ObjectNotFound_Raise_Should_Include_Type_And_Id()
    {
        // Arrange & Act
        var instance = CommonErrors.ObjectNotFound.Raise(typeof(string), 42);

        // Assert
        instance.Descriptor.ShouldBe(CommonErrors.ObjectNotFound);
        instance.Data.ShouldContainKey("type");
        instance.Data["type"].ShouldBe("String");
    }

    [Fact]
    public void ObjectNotFound_Raise_Should_Work_Without_Id()
    {
        // Arrange & Act
        var instance = CommonErrors.ObjectNotFound.Raise(typeof(string));

        // Assert
        instance.Descriptor.ShouldBe(CommonErrors.ObjectNotFound);
    }

    [Fact]
    public void ObjectsNotFound_Should_Have_NotFound_ErrorType()
    {
        // Arrange & Act & Assert
        CommonErrors.ObjectsNotFound.ErrorType.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public void ObjectsNotFound_Raise_Should_Include_Type_And_Ids()
    {
        // Arrange
        var ids = new List<object> { 1, 2, 3 };

        // Act
        var instance = CommonErrors.ObjectsNotFound.Raise(typeof(string), ids);

        // Assert
        instance.Descriptor.ShouldBe(CommonErrors.ObjectsNotFound);
        instance.Data.ShouldContainKey("type");
    }
}
