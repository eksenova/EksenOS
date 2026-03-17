using Eksen.TestBase;
using Shouldly;

namespace Eksen.Ulid.Tests;

public class UlidAttributeTests : EksenUnitTestBase
{
    [Fact]
    public void IsValid_Should_Return_True_For_Valid_Ulid_Object()
    {
        // Arrange
        var attribute = new UlidAttribute();
        var ulid = System.Ulid.NewUlid();

        // Act & Assert
        attribute.IsValid(ulid).ShouldBeTrue();
    }

    [Fact]
    public void IsValid_Should_Return_True_For_Valid_Ulid_String()
    {
        // Arrange
        var attribute = new UlidAttribute();
        var ulidString = System.Ulid.NewUlid().ToString();

        // Act & Assert
        attribute.IsValid(ulidString).ShouldBeTrue();
    }

    [Fact]
    public void IsValid_Should_Return_True_For_Null()
    {
        // Arrange
        var attribute = new UlidAttribute();

        // Act & Assert
        attribute.IsValid(null).ShouldBeTrue();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("12345")]
    [InlineData("")]
    public void IsValid_Should_Return_False_For_Invalid_String(string value)
    {
        // Arrange
        var attribute = new UlidAttribute();

        // Act & Assert
        attribute.IsValid(value).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_Should_Return_False_For_Non_Ulid_Object()
    {
        // Arrange
        var attribute = new UlidAttribute();

        // Act & Assert
        attribute.IsValid(12345).ShouldBeFalse();
    }

    [Fact]
    public void FormatErrorMessage_Should_Include_Field_Name()
    {
        // Arrange
        var attribute = new UlidAttribute();

        // Act
        var message = attribute.FormatErrorMessage("OrderId");

        // Assert
        message.ShouldContain("OrderId");
    }
}

public class UlidConstsTests : EksenUnitTestBase
{
    [Fact]
    public void Length_Should_Be_26()
    {
        // Assert
        UlidConsts.Length.ShouldBe(26);
    }
}

public class UlidValueInitializerTests : EksenUnitTestBase
{
    [Fact]
    public void New_Should_Return_Non_Empty_Ulid()
    {
        // Arrange & Act
        var ulid = UlidValueInitializer.New();

        // Assert
        ulid.ShouldNotBe(System.Ulid.Empty);
    }

    [Fact]
    public void New_Should_Return_Unique_Values()
    {
        // Arrange & Act
        var ulid1 = UlidValueInitializer.New();
        var ulid2 = UlidValueInitializer.New();

        // Assert
        ulid1.ShouldNotBe(ulid2);
    }

    [Fact]
    public void Empty_Should_Return_Empty_Ulid()
    {
        // Arrange & Act
        var empty = UlidValueInitializer.Empty;

        // Assert
        empty.ShouldBe(System.Ulid.Empty);
    }
}
