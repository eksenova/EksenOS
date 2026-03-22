using Eksen.ErrorHandling;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Authentication.ApiKeys.Tests;

public class ApiKeyValueTests : EksenUnitTestBase
{
    [Theory]
    [InlineData("abc123")]
    [InlineData("my-api-key-value")]
    [InlineData("a1b2c3d4e5f6")]
    public void Create_Should_Be_Successful(string value)
    {
        // Arrange & Act & Assert
        Should.NotThrow(() => ApiKeyValue.Create(value));
    }

    [Fact]
    public void Create_Should_Trim_Value()
    {
        // Arrange & Act
        var apiKeyValue = ApiKeyValue.Create("  test-key  ");

        // Assert
        apiKeyValue.Value.ShouldBe("test-key");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Value_Is_Empty(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => ApiKeyValue.Create(value!));
        exception.Descriptor.ShouldBe(ApiKeyErrors.EmptyApiKeyValue);
    }

    [Fact]
    public void Create_Should_Throw_When_Value_Exceeds_MaxLength()
    {
        // Arrange
        var longValue = new string('a', ApiKeyValue.MaxLength + 1);

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => ApiKeyValue.Create(longValue));
        exception.Descriptor.ShouldBe(ApiKeyErrors.ApiKeyValueOverflow);
    }

    [Fact]
    public void Parse_Should_Be_Successful()
    {
        // Arrange & Act
        var apiKeyValue = ApiKeyValue.Parse("test-key");

        // Assert
        apiKeyValue.Value.ShouldBe("test-key");
    }
}
