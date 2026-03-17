using Eksen.ErrorHandling;
using Eksen.Identity.Tenants;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Identity.Tests;

public class TenantErrorsTests : EksenUnitTestBase
{
    [Fact]
    public void Category_Should_Contain_Tenants()
    {
        TenantErrors.Category.ShouldEndWith(".Tenants");
    }

    [Fact]
    public void EmptyTenantName_Should_Be_Validation_Error()
    {
        TenantErrors.EmptyTenantName.ErrorType.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void TenantNameOverflow_Should_Be_Validation_Error()
    {
        TenantErrors.TenantNameOverflow.ErrorType.ShouldBe(ErrorType.Validation);
    }
}
