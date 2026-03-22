using Eksen.ErrorHandling;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Authentication.ApiKeys.Tests;

public class ApiKeyNameTests : EksenUnitTestBase
{
    [Theory]
    [InlineData("My API Key")]
    [InlineData("Production Key")]
    [InlineData("test")]
    public void Create_Should_Be_Successful(string value)
    {
        // Arrange & Act & Assert
        Should.NotThrow(() => ApiKeyName.Create(value));
    }

    [Fact]
    public void Create_Should_Trim_Value()
    {
        // Arrange & Act
        var name = ApiKeyName.Create("  My Key  ");

        // Assert
        name.Value.ShouldBe("My Key");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Value_Is_Empty(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => ApiKeyName.Create(value!));
        exception.Descriptor.ShouldBe(ApiKeyErrors.EmptyApiKeyName);
    }

    [Fact]
    public void Create_Should_Throw_When_Value_Exceeds_MaxLength()
    {
        // Arrange
        var longValue = new string('a', ApiKeyName.MaxLength + 1);

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => ApiKeyName.Create(longValue));
        exception.Descriptor.ShouldBe(ApiKeyErrors.ApiKeyNameOverflow);
    }

    [Fact]
    public void Parse_Should_Be_Successful()
    {
        // Arrange & Act
        var name = ApiKeyName.Parse("Test Key");

        // Assert
        name.Value.ShouldBe("Test Key");
    }
}
