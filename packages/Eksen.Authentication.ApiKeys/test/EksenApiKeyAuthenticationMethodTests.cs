using Eksen.TestBase;
using Shouldly;

namespace Eksen.Authentication.ApiKeys.Tests;

public class EksenApiKeyAuthenticationMethodTests : EksenUnitTestBase
{
    [Fact]
    public void CustomHeader_Should_Have_Default_Header_Name()
    {
        // Arrange & Act
        var method = EksenApiKeyAuthenticationMethods.CustomHeader;

        // Assert
        method.HeaderName.ShouldBe("X-API-KEY");
        method.Type.ShouldBe("CustomHeader");
    }

    [Fact]
    public void CustomHeader_WithHeaderName_Should_Set_Custom_Header()
    {
        // Arrange
        var method = EksenApiKeyAuthenticationMethods.CustomHeader;

        // Act
        var customMethod = method.WithHeaderName("X-My-Key");

        // Assert
        customMethod.HeaderName.ShouldBe("X-My-Key");
        customMethod.Type.ShouldBe("CustomHeader");
    }

    [Fact]
    public void AuthorizationHeader_Should_Have_Default_Scheme()
    {
        // Arrange & Act
        var method = EksenApiKeyAuthenticationMethods.AuthorizationHeader;

        // Assert
        method.Scheme.ShouldBe("Bearer");
        method.Type.ShouldBe("AuthorizationHeader");
    }

    [Fact]
    public void AuthorizationHeader_WithScheme_Should_Set_Custom_Scheme()
    {
        // Arrange
        var method = EksenApiKeyAuthenticationMethods.AuthorizationHeader;

        // Act
        var customMethod = method.WithScheme("ApiKey");

        // Assert
        customMethod.Scheme.ShouldBe("ApiKey");
        customMethod.Type.ShouldBe("AuthorizationHeader");
    }

    [Fact]
    public void CustomHeader_WithHeaderName_Should_Throw_When_Empty()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(
            () => EksenApiKeyAuthenticationMethods.CustomHeader.WithHeaderName(""));
    }

    [Fact]
    public void AuthorizationHeader_WithScheme_Should_Throw_When_Empty()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(
            () => EksenApiKeyAuthenticationMethods.AuthorizationHeader.WithScheme(""));
    }
}
