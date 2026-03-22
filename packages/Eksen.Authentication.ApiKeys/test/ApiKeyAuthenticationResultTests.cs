using Eksen.TestBase;
using Shouldly;

namespace Eksen.Authentication.ApiKeys.Tests;

public class ApiKeyAuthenticationResultTests : EksenUnitTestBase
{
    [Fact]
    public void Success_Should_Set_IsAuthenticated_And_Principal()
    {
        // Arrange
        var claims = new System.Security.Claims.ClaimsIdentity("Test");
        var principal = new System.Security.Claims.ClaimsPrincipal(claims);

        // Act
        var result = ApiKeyAuthenticationResult.Success(principal);

        // Assert
        result.IsAuthenticated.ShouldBeTrue();
        result.Principal.ShouldBe(principal);
        result.FailureReason.ShouldBeNull();
    }

    [Fact]
    public void Fail_Should_Set_FailureReason()
    {
        // Arrange & Act
        var result = ApiKeyAuthenticationResult.Fail("Invalid key");

        // Assert
        result.IsAuthenticated.ShouldBeFalse();
        result.Principal.ShouldBeNull();
        result.FailureReason.ShouldBe("Invalid key");
    }
}
