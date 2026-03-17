using Eksen.Localization.Formatting;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Localization.Tests;

public class SmartFormatMessageFormatterTests : EksenUnitTestBase
{
    private readonly SmartFormatMessageFormatter _formatter = new();

    [Fact]
    public void FormatMessage_Should_Replace_Single_Parameter()
    {
        // Arrange
        var message = "Hello, {Name}!";
        var parameters = new FormatParameter[]
        {
            new("Name", "World")
        };

        // Act
        var result = _formatter.FormatMessage(message, parameters);

        // Assert
        result.ShouldBe("Hello, World!");
    }

    [Fact]
    public void FormatMessage_Should_Replace_Multiple_Parameters()
    {
        // Arrange
        var message = "{FirstName} {LastName} is {Age} years old.";
        var parameters = new FormatParameter[]
        {
            new("FirstName", "John"),
            new("LastName", "Doe"),
            new("Age", 30)
        };

        // Act
        var result = _formatter.FormatMessage(message, parameters);

        // Assert
        result.ShouldBe("John Doe is 30 years old.");
    }

    [Fact]
    public void FormatMessage_Should_Return_Original_Message_When_No_Parameters()
    {
        // Arrange
        var message = "No placeholders here.";
        var parameters = Array.Empty<FormatParameter>();

        // Act
        var result = _formatter.FormatMessage(message, parameters);

        // Assert
        result.ShouldBe("No placeholders here.");
    }

    [Fact]
    public void FormatMessage_Should_Handle_Null_Parameter_Value()
    {
        // Arrange
        var message = "Value is {Value}.";
        var parameters = new FormatParameter[]
        {
            new("Value", null)
        };

        // Act
        var result = _formatter.FormatMessage(message, parameters);

        // Assert
        result.ShouldBe("Value is .");
    }

    [Fact]
    public void FormatMessage_Should_Handle_Numeric_Formatting()
    {
        // Arrange
        var message = "Total: {Amount}";
        var parameters = new FormatParameter[]
        {
            new("Amount", 1234.56m)
        };

        // Act
        var result = _formatter.FormatMessage(message, parameters);

        // Assert
        result.ShouldContain("1234");
    }

    [Fact]
    public void FormatMessage_Should_Handle_Boolean_Parameter()
    {
        // Arrange
        var message = "Active: {IsActive}";
        var parameters = new FormatParameter[]
        {
            new("IsActive", true)
        };

        // Act
        var result = _formatter.FormatMessage(message, parameters);

        // Assert
        result.ShouldBe("Active: True");
    }

    [Fact]
    public void FormatMessage_Should_Handle_Repeated_Parameters()
    {
        // Arrange
        var message = "{Name} says hello. {Name} is great.";
        var parameters = new FormatParameter[]
        {
            new("Name", "Alice")
        };

        // Act
        var result = _formatter.FormatMessage(message, parameters);

        // Assert
        result.ShouldBe("Alice says hello. Alice is great.");
    }
}
