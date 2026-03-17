using Eksen.Permissions.EntityFrameworkCore.Tests.Fakes;
using Eksen.TestBase;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Eksen.Permissions.EntityFrameworkCore.Tests;

public class TypeConfigurationTests : EksenUnitTestBase, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PermissionsTestDbContext _dbContext;

    public TypeConfigurationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PermissionsTestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new PermissionsTestDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void PermissionDefinition_Should_Have_Correct_Table_Name()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(PermissionDefinition));
        entityType.ShouldNotBeNull();
        entityType.GetTableName().ShouldBe("PermissionDefinitions");
    }

    [Fact]
    public void PermissionDefinition_Id_Should_Be_String_With_MaxLength()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(PermissionDefinition))!;
        var idProp = entityType.FindProperty(nameof(PermissionDefinition.Id))!;
        idProp.GetMaxLength().ShouldBe(PermissionDefinitionId.Length);
        idProp.ClrType.ShouldBe(typeof(PermissionDefinitionId));
    }

    [Fact]
    public void PermissionDefinition_Name_Should_Be_Required_With_MaxLength()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(PermissionDefinition))!;
        var nameProp = entityType.FindProperty(nameof(PermissionDefinition.Name))!;
        nameProp.IsNullable.ShouldBeFalse();
        nameProp.GetMaxLength().ShouldBe(PermissionName.MaxLength);
    }

    [Fact]
    public void RolePermission_Should_Have_Correct_Table_Name()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(EksenRolePermission<TestRole, TestTenant>));
        entityType.ShouldNotBeNull();
        entityType.GetTableName().ShouldBe("RolePermissions");
    }

    [Fact]
    public void RolePermission_Should_Have_Primary_Key()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(EksenRolePermission<TestRole, TestTenant>))!;
        var pk = entityType.FindPrimaryKey();
        pk.ShouldNotBeNull();
        pk.Properties.Select(p => p.Name).ShouldContain(nameof(EksenRolePermission<TestRole, TestTenant>.Id));
    }

    [Fact]
    public void RolePermission_Should_Have_Foreign_Keys()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(EksenRolePermission<TestRole, TestTenant>))!;
        var fks = entityType.GetForeignKeys().ToList();

        fks.ShouldContain(fk => fk.PrincipalEntityType.ClrType == typeof(TestRole));
        fks.ShouldContain(fk => fk.PrincipalEntityType.ClrType == typeof(PermissionDefinition));
    }

    [Fact]
    public void UserPermission_Should_Have_Correct_Table_Name()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(EksenUserPermission<TestUser, TestTenant>));
        entityType.ShouldNotBeNull();
        entityType.GetTableName().ShouldBe("UserPermissions");
    }

    [Fact]
    public void UserPermission_Should_Have_Foreign_Keys()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(EksenUserPermission<TestUser, TestTenant>))!;
        var fks = entityType.GetForeignKeys().ToList();

        fks.ShouldContain(fk => fk.PrincipalEntityType.ClrType == typeof(TestUser));
        fks.ShouldContain(fk => fk.PrincipalEntityType.ClrType == typeof(PermissionDefinition));
    }

    [Fact]
    public void UserRole_Should_Have_Correct_Table_Name()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(EksenUserRole<TestUser, TestRole, TestTenant>));
        entityType.ShouldNotBeNull();
        entityType.GetTableName().ShouldBe("UserRoles");
    }

    [Fact]
    public void UserRole_Should_Have_Foreign_Keys()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(EksenUserRole<TestUser, TestRole, TestTenant>))!;
        var fks = entityType.GetForeignKeys().ToList();

        fks.ShouldContain(fk => fk.PrincipalEntityType.ClrType == typeof(TestUser));
        fks.ShouldContain(fk => fk.PrincipalEntityType.ClrType == typeof(TestRole));
    }

    [Fact]
    public void RolePermission_Id_Should_Have_MaxLength()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(EksenRolePermission<TestRole, TestTenant>))!;
        var idProp = entityType.FindProperty(nameof(EksenRolePermission<TestRole, TestTenant>.Id))!;
        idProp.GetMaxLength().ShouldBe(EksenRolePermissionId.Length);
    }

    [Fact]
    public void UserPermission_Id_Should_Have_MaxLength()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(EksenUserPermission<TestUser, TestTenant>))!;
        var idProp = entityType.FindProperty(nameof(EksenUserPermission<TestUser, TestTenant>.Id))!;
        idProp.GetMaxLength().ShouldBe(EksenUserPermissionId.Length);
    }

    [Fact]
    public void UserRole_Id_Should_Have_MaxLength()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(EksenUserRole<TestUser, TestRole, TestTenant>))!;
        var idProp = entityType.FindProperty(nameof(EksenUserRole<TestUser, TestRole, TestTenant>.Id))!;
        idProp.GetMaxLength().ShouldBe(EksenUserRoleId.Length);
    }

    [Fact]
    public void RolePermission_Should_Have_Indexes()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(EksenRolePermission<TestRole, TestTenant>))!;
        var indexes = entityType.GetIndexes().ToList();
        indexes.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void UserPermission_Should_Have_Indexes()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(EksenUserPermission<TestUser, TestTenant>))!;
        var indexes = entityType.GetIndexes().ToList();
        indexes.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void UserRole_Should_Have_Indexes()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(EksenUserRole<TestUser, TestRole, TestTenant>))!;
        var indexes = entityType.GetIndexes().ToList();
        indexes.Count.ShouldBeGreaterThanOrEqualTo(2);
    }
}
