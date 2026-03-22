using Eksen.TestBase;
using Shouldly;

namespace Eksen.Authentication.ApiKeys.Tests;

public class GuidApiKeyGeneratorTests : EksenUnitTestBase
{
    [Fact]
    public void Generate_Should_Return_Non_Empty_String()
    {
        // Arrange
        var generator = new GuidApiKeyGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Generate_Should_Return_32_Character_String()
    {
        // Arrange
        var generator = new GuidApiKeyGenerator();

        // Act
        var result = generator.Generate();

        // Assert
        result.Length.ShouldBe(32);
    }

    [Fact]
    public void Generate_Should_Return_Unique_Values()
    {
        // Arrange
        var generator = new GuidApiKeyGenerator();

        // Act
        var key1 = generator.Generate();
        var key2 = generator.Generate();

        // Assert
        key1.ShouldNotBe(key2);
    }
}
