using Eksen.Scalar;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Scalar.Tests;

/// <summary>
/// Verifies the per-plugin option defaults are sensible and carry nothing product-specific, so the
/// package stays a generic, reusable building block.
/// </summary>
public class EksenScalarOptionsTests : EksenUnitTestBase
{
    #region Logo defaults

    [Fact]
    public void Logo_Defaults_Should_Be_Unbranded()
    {
        var options = new ScalarLogoOptions();

        options.LogoUrl.ShouldBeNull();
        options.LogoAltText.ShouldBe("Logo");
        options.FooterText.ShouldBeNull();
        options.FooterTitle.ShouldBeNull();
        options.HideMcpControls.ShouldBeTrue();
        options.CollapseSidebarCategories.ShouldBeTrue();
    }

    #endregion

    #region Token-body / impersonation defaults

    [Fact]
    public void TokenBody_TokenEndpoint_Should_Default_To_Connect_Token()
    {
        var options = new ScalarTokenBodyOptions();

        options.TokenEndpoint.ShouldBe("/connect/token");
    }

    [Fact]
    public void Impersonation_Defaults_Should_Be_Sensible()
    {
        var options = new ScalarImpersonationOptions();

        options.TokenEndpoint.ShouldBe("/connect/token");
        options.GrantType.ShouldBe("tenant_impersonation");
        options.ClientId.ShouldBe("scalar");
        options.HostClaim.ShouldBe("is_host");
        options.ImpersonatingClaim.ShouldBe("is_impersonating");
    }

    #endregion

    #region Internal-auth defaults

    [Fact]
    public void InternalAuth_Defaults_Should_Be_Sensible()
    {
        var options = new ScalarInternalAuthOptions();

        options.InternalDocumentPath.ShouldBe("/openapi/internal");
        options.TokenEndpoint.ShouldBe("/connect/token");
        options.ClientId.ShouldBe("scalar");
        options.HostClaim.ShouldBe("is_host");
        options.ClientSecret.ShouldBeNull();
        options.DefaultUsername.ShouldBeNull();
    }

    #endregion

    #region Nothing product-specific is hardcoded

    [Fact]
    public void Defaults_Should_Not_Contain_Eksenova_Specific_Values()
    {
        new ScalarLogoOptions().LogoUrl.ShouldBeNull();
        new ScalarLogoOptions().FooterText.ShouldBeNull();
        new ScalarInternalAuthOptions().DefaultUsername.ShouldBeNull();
        new ScalarInternalAuthOptions().BrandText.ShouldNotContain("Eksenova");
    }

    #endregion
}
