using Eksen.TestBase;
using Shouldly;

namespace Eksen.Permissions.Tests;

public class PermissionDefinitionTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_Should_Create_PermissionDefinition()
    {
        // Arrange
        var name = PermissionName.Create("Orders.Create");

        // Act
        var definition = new PermissionDefinition(name);

        // Assert
        definition.Id.ShouldNotBe(PermissionDefinitionId.Empty);
        definition.Name.ShouldBe(name);
        definition.IsDeleted.ShouldBeFalse();
        definition.IsDisabled.ShouldBeFalse();
    }

    [Fact]
    public void SetIsEnabled_Should_Disable_When_False()
    {
        // Arrange
        var definition = new PermissionDefinition(PermissionName.Create("Orders.Create"));

        // Act
        definition.SetIsEnabled(false);

        // Assert
        definition.IsDisabled.ShouldBeTrue();
    }

    [Fact]
    public void SetIsEnabled_Should_Enable_When_True()
    {
        // Arrange
        var definition = new PermissionDefinition(PermissionName.Create("Orders.Create"));
        definition.SetIsEnabled(false);

        // Act
        definition.SetIsEnabled(true);

        // Assert
        definition.IsDisabled.ShouldBeFalse();
    }

    [Fact]
    public void SetName_Should_Update_Name()
    {
        // Arrange
        var definition = new PermissionDefinition(PermissionName.Create("Orders.Create"));
        var newName = PermissionName.Create("Orders.Update");

        // Act
        definition.SetName(newName);

        // Assert
        definition.Name.ShouldBe(newName);
    }
}
