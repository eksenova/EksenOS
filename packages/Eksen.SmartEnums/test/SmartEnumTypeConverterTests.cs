using System.ComponentModel;
using Eksen.SmartEnums;
using Eksen.SmartEnums.Tests.Fakes;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.SmartEnums.Tests;

public class SmartEnumTypeConverterTests : EksenUnitTestBase
{
    private readonly SmartEnumTypeConverter<TestColor> _converter = new();

    [Fact]
    public void CanConvertFrom_Should_Return_True_For_String()
    {
        // Arrange & Act & Assert
        _converter.CanConvertFrom(null, typeof(string)).ShouldBeTrue();
    }

    [Fact]
    public void CanConvertFrom_Should_Return_True_For_Int()
    {
        // Arrange & Act & Assert
        _converter.CanConvertFrom(null, typeof(int)).ShouldBeTrue();
    }

    [Fact]
    public void CanConvertFrom_Should_Return_False_For_Other_Types()
    {
        // Arrange & Act & Assert
        _converter.CanConvertFrom(null, typeof(double)).ShouldBeFalse();
    }

    [Fact]
    public void CanConvertTo_Should_Return_True_For_String()
    {
        // Arrange & Act & Assert
        _converter.CanConvertTo(null, typeof(string)).ShouldBeTrue();
    }

    [Fact]
    public void CanConvertTo_Should_Return_True_For_Int()
    {
        // Arrange & Act & Assert
        _converter.CanConvertTo(null, typeof(int)).ShouldBeTrue();
    }

    [Fact]
    public void ConvertFrom_Should_Parse_String_To_Enumeration()
    {
        // Arrange & Act
        var result = _converter.ConvertFrom(null, null, "Red");

        // Assert
        result.ShouldBe(TestColor.Red);
    }

    [Fact]
    public void ConvertTo_String_Should_Return_Code()
    {
        // Arrange & Act
        var result = _converter.ConvertTo(null, null, TestColor.Blue, typeof(string));

        // Assert
        result.ShouldBe("Blue");
    }
}
