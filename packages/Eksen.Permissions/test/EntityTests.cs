using Eksen.Permissions.Tests.Fakes;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Permissions.Tests;

public class EksenRolePermissionTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_Should_Create_RolePermission()
    {
        // Arrange
        var role = new FakeRole();
        var definition = new PermissionDefinition(PermissionName.Create("Orders.Create"));
        var tenant = new FakeTenant();

        // Act
        var rolePermission = new EksenRolePermission<FakeRole, FakeTenant>(role, definition, tenant);

        // Assert
        rolePermission.Id.ShouldNotBe(EksenRolePermissionId.Empty);
        rolePermission.Role.ShouldBe(role);
        rolePermission.PermissionDefinition.ShouldBe(definition);
        rolePermission.Tenant.ShouldBe(tenant);
    }

    [Fact]
    public void Constructor_Should_Allow_Null_Tenant()
    {
        // Arrange
        var role = new FakeRole();
        var definition = new PermissionDefinition(PermissionName.Create("Orders.Create"));

        // Act
        var rolePermission = new EksenRolePermission<FakeRole, FakeTenant>(role, definition, tenant: null);

        // Assert
        rolePermission.Tenant.ShouldBeNull();
    }
}

public class EksenUserPermissionTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_Should_Create_UserPermission()
    {
        // Arrange
        var user = new FakeUser();
        var definition = new PermissionDefinition(PermissionName.Create("Orders.Create"));
        var tenant = new FakeTenant();

        // Act
        var userPermission = new EksenUserPermission<FakeUser, FakeTenant>(user, definition, tenant);

        // Assert
        userPermission.Id.ShouldNotBe(EksenUserPermissionId.Empty);
        userPermission.User.ShouldBe(user);
        userPermission.PermissionDefinition.ShouldBe(definition);
        userPermission.Tenant.ShouldBe(tenant);
    }

    [Fact]
    public void Constructor_Should_Allow_Null_Tenant()
    {
        // Arrange
        var user = new FakeUser();
        var definition = new PermissionDefinition(PermissionName.Create("Orders.Create"));

        // Act
        var userPermission = new EksenUserPermission<FakeUser, FakeTenant>(user, definition, tenant: null);

        // Assert
        userPermission.Tenant.ShouldBeNull();
    }
}

public class EksenUserRoleTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_Should_Create_UserRole()
    {
        // Arrange
        var user = new FakeUser();
        var role = new FakeRole();
        var tenant = new FakeTenant();

        // Act
        var userRole = new EksenUserRole<FakeUser, FakeRole, FakeTenant>(user, role, tenant);

        // Assert
        userRole.Id.ShouldNotBe(EksenUserRoleId.Empty);
        userRole.User.ShouldBe(user);
        userRole.Role.ShouldBe(role);
        userRole.Tenant.ShouldBe(tenant);
    }

    [Fact]
    public void Constructor_Should_Allow_Null_Tenant()
    {
        // Arrange
        var user = new FakeUser();
        var role = new FakeRole();

        // Act
        var userRole = new EksenUserRole<FakeUser, FakeRole, FakeTenant>(user, role, tenant: null);

        // Assert
        userRole.Tenant.ShouldBeNull();
    }
}
