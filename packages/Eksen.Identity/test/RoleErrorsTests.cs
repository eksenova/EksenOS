using Eksen.ErrorHandling;
using Eksen.Identity.Roles;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Identity.Tests;

public class RoleErrorsTests : EksenUnitTestBase
{
    [Fact]
    public void Category_Should_Contain_Roles()
    {
        RoleErrors.Category.ShouldEndWith(".Roles");
    }

    [Fact]
    public void RoleNameEmpty_Should_Be_Validation_Error()
    {
        RoleErrors.RoleNameEmpty.ErrorType.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void RoleNameOverflow_Should_Be_Validation_Error()
    {
        RoleErrors.RoleNameOverflow.ErrorType.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void RoleNameAlreadyExists_Should_Be_Validation_Error()
    {
        RoleErrors.RoleNameAlreadyExists.ErrorType.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void RoleNameAlreadyExists_Should_Raise_ErrorInstance_With_RoleName()
    {
        // Arrange
        var roleName = RoleName.Create("Admin");

        // Act
        var error = RoleErrors.RoleNameAlreadyExists.Raise(roleName);

        // Assert
        error.ShouldNotBeNull();
        error.Descriptor.ShouldBe(RoleErrors.RoleNameAlreadyExists);
    }

    [Fact]
    public void CannotDeleteWithUsers_Should_Be_Validation_Error()
    {
        RoleErrors.CannotDeleteWithUsers.ErrorType.ShouldBe(ErrorType.Validation);
    }
}
