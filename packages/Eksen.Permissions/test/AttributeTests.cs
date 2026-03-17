using Eksen.TestBase;
using Shouldly;

namespace Eksen.Permissions.Tests;

public class DisableTenantSeedingTests : EksenUnitTestBase
{
    [Fact]
    public void Default_Constructor_Should_Set_IsDisabled_True()
    {
        // Act
        var attribute = new DisableTenantSeeding();

        // Assert
        attribute.IsDisabled.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_Should_Set_IsDisabled_To_Provided_Value()
    {
        // Act
        var attribute = new DisableTenantSeeding(isDisabled: false);

        // Assert
        attribute.IsDisabled.ShouldBeFalse();
    }

    [Fact]
    public void Attribute_Should_Target_Field_And_Class()
    {
        // Arrange
        var attrUsage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(DisableTenantSeeding), typeof(AttributeUsageAttribute))!;

        // Assert
        attrUsage.ValidOn.ShouldBe(AttributeTargets.Field | AttributeTargets.Class);
        attrUsage.Inherited.ShouldBeFalse();
    }
}

public class DefinedPermissionTests : EksenUnitTestBase
{
    [Fact]
    public void Record_Should_Store_Name_And_IsTenantSeedDisabled()
    {
        // Arrange
        var name = PermissionName.Create("Orders.Create");

        // Act
        var defined = new DefinedPermission(name, IsTenantSeedDisabled: true);

        // Assert
        defined.Name.ShouldBe(name);
        defined.IsTenantSeedDisabled.ShouldBeTrue();
    }

    [Fact]
    public void Record_Should_Default_IsTenantSeedDisabled_To_False()
    {
        // Arrange
        var name = PermissionName.Create("Orders.Create");

        // Act
        var defined = new DefinedPermission(name, IsTenantSeedDisabled: false);

        // Assert
        defined.IsTenantSeedDisabled.ShouldBeFalse();
    }
}
