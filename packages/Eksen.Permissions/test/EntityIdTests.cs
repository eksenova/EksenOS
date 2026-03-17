using Eksen.TestBase;
using Shouldly;

namespace Eksen.Permissions.Tests;

public class EntityIdTests : EksenUnitTestBase
{
    [Fact]
    public void PermissionDefinitionId_Should_Wrap_Ulid()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();

        // Act
        var id = new PermissionDefinitionId(ulid);

        // Assert
        id.Value.ShouldBe(ulid);
    }

    [Fact]
    public void PermissionDefinitionId_Empty_Should_Exist()
    {
        // Assert
        PermissionDefinitionId.Empty.ShouldNotBeNull();
    }

    [Fact]
    public void EksenRolePermissionId_Should_Wrap_Ulid()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();

        // Act
        var id = new EksenRolePermissionId(ulid);

        // Assert
        id.Value.ShouldBe(ulid);
    }

    [Fact]
    public void EksenUserPermissionId_Should_Wrap_Ulid()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();

        // Act
        var id = new EksenUserPermissionId(ulid);

        // Assert
        id.Value.ShouldBe(ulid);
    }

    [Fact]
    public void EksenUserRoleId_Should_Wrap_Ulid()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();

        // Act
        var id = new EksenUserRoleId(ulid);

        // Assert
        id.Value.ShouldBe(ulid);
    }

    [Fact]
    public void Equal_Ids_Should_Be_Equal()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();
        var id1 = new EksenRolePermissionId(ulid);
        var id2 = new EksenRolePermissionId(ulid);

        // Assert
        id1.ShouldBe(id2);
    }

    [Fact]
    public void Different_Ids_Should_Not_Be_Equal()
    {
        // Arrange
        var id1 = new EksenRolePermissionId(System.Ulid.NewUlid());
        var id2 = new EksenRolePermissionId(System.Ulid.NewUlid());

        // Assert
        id1.ShouldNotBe(id2);
    }

    [Fact]
    public void ToString_Should_Return_Ulid_String()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();
        var id = new EksenUserPermissionId(ulid);

        // Assert
        id.ToString().ShouldContain(ulid.ToString());
    }
}
