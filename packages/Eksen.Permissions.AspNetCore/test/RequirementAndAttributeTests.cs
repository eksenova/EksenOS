using Eksen.Permissions.AspNetCore;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Permissions.AspNetCore.Tests;

public class PermissionAuthorizationRequirementTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_Should_Set_Permission()
    {
        // Arrange
        var permission = new DefinedPermission(PermissionName.Create("Orders.Create"), false);

        // Act
        var requirement = new PermissionAuthorizationRequirement(permission);

        // Assert
        requirement.Permission.ShouldBe(permission);
        requirement.Permission.Name.Value.ShouldBe("Orders.Create");
    }
}

public class BindPermissionAttributeTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_Should_Set_PermissionName()
    {
        // Act
        var attr = new BindPermissionAttribute("Orders.Create");

        // Assert
        attr.PermissionName.ShouldBe("Orders.Create");
    }

    [Fact]
    public void Attribute_Should_Allow_Multiple()
    {
        // Assert
        var usage = typeof(BindPermissionAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.AllowMultiple.ShouldBeTrue();
    }

    [Fact]
    public void Attribute_Should_Target_Fields_And_Properties()
    {
        // Assert
        var usage = typeof(BindPermissionAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.ShouldBe(AttributeTargets.Field | AttributeTargets.Property);
    }
}
