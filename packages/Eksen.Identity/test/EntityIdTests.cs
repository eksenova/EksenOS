using Eksen.Identity.Roles;
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Identity.Tests;

public class EntityIdTests : EksenUnitTestBase
{
    [Fact]
    public void EksenRoleId_Should_Wrap_Ulid_Value()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();

        // Act
        var roleId = new EksenRoleId(ulid);

        // Assert
        roleId.Value.ShouldBe(ulid);
    }

    [Fact]
    public void EksenRoleId_Should_Support_Equality()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();

        // Act
        var id1 = new EksenRoleId(ulid);
        var id2 = new EksenRoleId(ulid);

        // Assert
        id1.ShouldBe(id2);
    }

    [Fact]
    public void EksenRoleId_Should_Differ_For_Different_Values()
    {
        // Arrange & Act
        var id1 = new EksenRoleId(System.Ulid.NewUlid());
        var id2 = new EksenRoleId(System.Ulid.NewUlid());

        // Assert
        id1.ShouldNotBe(id2);
    }

    [Fact]
    public void EksenTenantId_Should_Wrap_Ulid_Value()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();

        // Act
        var tenantId = new EksenTenantId(ulid);

        // Assert
        tenantId.Value.ShouldBe(ulid);
    }

    [Fact]
    public void EksenTenantId_Should_Support_Equality()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();

        // Act
        var id1 = new EksenTenantId(ulid);
        var id2 = new EksenTenantId(ulid);

        // Assert
        id1.ShouldBe(id2);
    }

    [Fact]
    public void EksenTenantId_Should_Differ_For_Different_Values()
    {
        // Arrange & Act
        var id1 = new EksenTenantId(System.Ulid.NewUlid());
        var id2 = new EksenTenantId(System.Ulid.NewUlid());

        // Assert
        id1.ShouldNotBe(id2);
    }

    [Fact]
    public void EksenUserId_Should_Wrap_Ulid_Value()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();

        // Act
        var userId = new EksenUserId(ulid);

        // Assert
        userId.Value.ShouldBe(ulid);
    }

    [Fact]
    public void EksenUserId_Should_Support_Equality()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();

        // Act
        var id1 = new EksenUserId(ulid);
        var id2 = new EksenUserId(ulid);

        // Assert
        id1.ShouldBe(id2);
    }

    [Fact]
    public void EksenUserId_Should_Differ_For_Different_Values()
    {
        // Arrange & Act
        var id1 = new EksenUserId(System.Ulid.NewUlid());
        var id2 = new EksenUserId(System.Ulid.NewUlid());

        // Assert
        id1.ShouldNotBe(id2);
    }

    [Fact]
    public void EksenRoleId_ToString_Should_Return_Ulid_String()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();
        var roleId = new EksenRoleId(ulid);

        // Assert
        roleId.ToString().ShouldContain(ulid.ToString());
    }

    [Fact]
    public void EksenTenantId_ToString_Should_Return_Ulid_String()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();
        var tenantId = new EksenTenantId(ulid);

        // Assert
        tenantId.ToString().ShouldContain(ulid.ToString());
    }

    [Fact]
    public void EksenUserId_ToString_Should_Return_Ulid_String()
    {
        // Arrange
        var ulid = System.Ulid.NewUlid();
        var userId = new EksenUserId(ulid);

        // Assert
        userId.ToString().ShouldContain(ulid.ToString());
    }
}
