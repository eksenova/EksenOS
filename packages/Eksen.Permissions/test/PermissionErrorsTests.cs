using Eksen.ErrorHandling;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Permissions.Tests;

public class PermissionErrorsTests : EksenUnitTestBase
{
    [Fact]
    public void Category_Should_Be_Permissions()
    {
        // Assert
        PermissionErrors.Category.ShouldContain("Permissions");
    }

    [Fact]
    public void EmptyPermissionName_Should_Be_Validation_Error()
    {
        // Assert
        PermissionErrors.EmptyPermissionName.ErrorType.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void EmptyPermissionName_Should_Raise()
    {
        // Act & Assert
        Should.NotThrow(() => PermissionErrors.EmptyPermissionName.Raise());
    }

    [Fact]
    public void PermissionNameOverflow_Should_Be_Validation_Error()
    {
        // Assert
        PermissionErrors.PermissionNameOverflow.ErrorType.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void PermissionNameOverflow_Should_Raise()
    {
        // Act & Assert
        Should.NotThrow(() => PermissionErrors.PermissionNameOverflow.Raise("test", 50));
    }
}
