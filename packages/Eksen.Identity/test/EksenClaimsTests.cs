using Eksen.Identity.Claims;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Identity.Tests;

public class EksenClaimsTests : EksenUnitTestBase
{
    [Fact]
    public void TenantId_Should_Be_Expected_Value()
    {
        EksenClaims.TenantId.ShouldBe("eks_tenant_id");
    }

    [Fact]
    public void TenantName_Should_Be_Expected_Value()
    {
        EksenClaims.TenantName.ShouldBe("eks_tenant_name");
    }

    [Fact]
    public void OriginalTenantId_Should_Be_Expected_Value()
    {
        EksenClaims.OriginalTenantId.ShouldBe("eks_original_tenant_id");
    }

    [Fact]
    public void OriginalTenantName_Should_Be_Expected_Value()
    {
        EksenClaims.OriginalTenantName.ShouldBe("eks_original_tenant_name");
    }

    [Fact]
    public void IsImpersonating_Should_Be_Expected_Value()
    {
        EksenClaims.IsImpersonating.ShouldBe("eks_is_impersonating");
    }
}
