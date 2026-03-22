using Eksen.TestBase;
using Shouldly;

namespace Eksen.Authentication.ApiKeys.Tests;

public class EntityIdTests : EksenUnitTestBase
{
    [Fact]
    public void ApiKeyValue_MaxLength_Should_Be_Expected()
    {
        // Arrange & Act & Assert
        ApiKeyValue.MaxLength.ShouldBe(128);
    }

    [Fact]
    public void ApiKeyName_MaxLength_Should_Be_Expected()
    {
        // Arrange & Act & Assert
        ApiKeyName.MaxLength.ShouldBe(100);
    }
}
