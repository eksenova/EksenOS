using Eksen.Localization.Formatting;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Localization.Tests;

public class FormatParameterTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_Should_Set_Key_And_Value()
    {
        // Arrange & Act
        var param = new FormatParameter("Name", "Alice");

        // Assert
        param.Key.ShouldBe("Name");
        param.Value.ShouldBe("Alice");
    }

    [Fact]
    public void Constructor_Should_Allow_Null_Value()
    {
        // Arrange & Act
        var param = new FormatParameter("Key", null);

        // Assert
        param.Key.ShouldBe("Key");
        param.Value.ShouldBeNull();
    }

    [Fact]
    public void Equality_Should_Work_For_Same_Key_And_Value()
    {
        // Arrange
        var param1 = new FormatParameter("Name", "Alice");
        var param2 = new FormatParameter("Name", "Alice");

        // Act & Assert
        param1.ShouldBe(param2);
    }

    [Fact]
    public void Equality_Should_Fail_For_Different_Values()
    {
        // Arrange
        var param1 = new FormatParameter("Name", "Alice");
        var param2 = new FormatParameter("Name", "Bob");

        // Act & Assert
        param1.ShouldNotBe(param2);
    }

    [Fact]
    public void Equality_Should_Fail_For_Different_Keys()
    {
        // Arrange
        var param1 = new FormatParameter("Name", "Alice");
        var param2 = new FormatParameter("Title", "Alice");

        // Act & Assert
        param1.ShouldNotBe(param2);
    }
}
