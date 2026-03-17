using Eksen.TestBase;
using Shouldly;

namespace Eksen.EventBus.Dashboard.Tests;

public class EventBusDashboardOptionsTests : EksenUnitTestBase
{
    [Fact]
    public void Defaults_Should_Have_Expected_Values()
    {
        // Arrange & Act
        var options = new EventBusDashboardOptions();

        // Assert
        options.RoutePrefix.ShouldBe("eksen-eventbus");
        options.Title.ShouldBe("Eksen EventBus Dashboard");
        options.Auth.ShouldNotBeNull();
        options.Auth.AuthMode.ShouldBe(EventBusDashboardAuthMode.None);
    }

    [Fact]
    public void AuthOptions_Should_Default_To_None()
    {
        // Arrange & Act
        var authOptions = new EventBusDashboardAuthOptions();

        // Assert
        authOptions.AuthMode.ShouldBe(EventBusDashboardAuthMode.None);
        authOptions.Username.ShouldBeNull();
        authOptions.Password.ShouldBeNull();
        authOptions.OpenIdConnect.ShouldBeNull();
        authOptions.CustomAuthorize.ShouldBeNull();
    }

    [Fact]
    public void OpenIdConnectOptions_Should_Have_Defaults()
    {
        // Arrange & Act
        var oidcOptions = new OpenIdConnectOptions
        {
            Authority = "https://auth.example.com",
            ClientId = "my-client"
        };

        // Assert
        oidcOptions.Scopes.ShouldBe(["openid", "profile"]);
        oidcOptions.CallbackPath.ShouldBe("/eksen-eventbus/auth/callback");
        oidcOptions.ClientSecret.ShouldBeNull();
    }

    [Fact]
    public void AuthMode_Enum_Should_Have_Expected_Values()
    {
        // Assert
        ((int)EventBusDashboardAuthMode.None).ShouldBe(0);
        ((int)EventBusDashboardAuthMode.BasicAuth).ShouldBe(1);
        ((int)EventBusDashboardAuthMode.OpenIdConnect).ShouldBe(2);
        ((int)EventBusDashboardAuthMode.Custom).ShouldBe(3);
    }
}
