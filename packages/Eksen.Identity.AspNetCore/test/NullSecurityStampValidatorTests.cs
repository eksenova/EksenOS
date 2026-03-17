using Eksen.Identity.AspNetCore.Security;
using Eksen.TestBase;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace Eksen.Identity.AspNetCore.Tests;

public class NullSecurityStampValidatorTests : EksenUnitTestBase
{
    [Fact]
    public async Task ValidateAsync_Should_Complete_Without_Throwing()
    {
        // Arrange
        var validator = new NullSecurityStampValidator();
        var httpContext = new DefaultHttpContext();
        var scheme = new Microsoft.AspNetCore.Authentication.AuthenticationScheme(
            "TestScheme",
            "Test",
            typeof(CookieAuthenticationHandler));
        var options = new CookieAuthenticationOptions();
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(
            new System.Security.Claims.ClaimsPrincipal(),
            "TestScheme");
        var context = new CookieValidatePrincipalContext(
            httpContext,
            scheme,
            options,
            ticket);

        // Act & Assert - should not throw
        await validator.ValidateAsync(context);
    }
}
