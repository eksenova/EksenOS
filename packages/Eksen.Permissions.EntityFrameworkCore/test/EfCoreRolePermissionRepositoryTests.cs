using Eksen.Identity.Roles;
using Eksen.Permissions.EntityFrameworkCore.Tests.Fakes;
using Eksen.TestBase;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Eksen.Permissions.EntityFrameworkCore.Tests;

public class EfCoreRolePermissionRepositoryTests : EksenUnitTestBase, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PermissionsTestDbContext _dbContext;
    private readonly EfCoreEksenRolePermissionRepository<PermissionsTestDbContext, TestRole, TestTenant> _sut;

    public EfCoreRolePermissionRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PermissionsTestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new PermissionsTestDbContext(options);
        _dbContext.Database.EnsureCreated();
        _sut = new EfCoreEksenRolePermissionRepository<PermissionsTestDbContext, TestRole, TestTenant>(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private async Task<(TestRole Role, PermissionDefinition Def)> SeedRolePermissionAsync(
        string permName, string roleName = "TestRole")
    {
        var role = new TestRole { Name = RoleName.Create(roleName) };
        _dbContext.Roles.Add(role);

        var def = new PermissionDefinition(PermissionName.Create(permName));
        _dbContext.PermissionDefinitions.Add(def);

        var rp = new EksenRolePermission<TestRole, TestTenant>(role, def, null);
        _dbContext.RolePermissions.Add(rp);

        await _dbContext.SaveChangesAsync();
        return (role, def);
    }

    [Fact]
    public async Task GetByRoleIdAsync_Should_Return_Permissions_For_Role()
    {
        // Arrange
        var (role, def) = await SeedRolePermissionAsync("Orders.Create");

        // Act
        var result = await _sut.GetByRoleIdAsync(role.Id);

        // Assert
        result.Count.ShouldBe(1);
        result.First().Name.Value.ShouldBe("Orders.Create");
    }

    [Fact]
    public async Task GetByRoleIdAsync_Should_Return_Empty_When_No_Permissions()
    {
        // Arrange
        var role = new TestRole { Name = RoleName.Create("EmptyRole") };
        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetByRoleIdAsync(role.Id);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByRoleIdsAsync_Should_Return_Permissions_For_Multiple_Roles()
    {
        // Arrange
        var (role1, _) = await SeedRolePermissionAsync("Orders.Create", "Role1");
        var (role2, _) = await SeedRolePermissionAsync("Users.Manage", "Role2");

        // Act
        var result = await _sut.GetByRoleIdsAsync([role1.Id, role2.Id]);

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetByRoleIdsAsync_Should_Return_Empty_When_Empty_Ids()
    {
        // Act
        var result = await _sut.GetByRoleIdsAsync([]);

        // Assert
        result.ShouldBeEmpty();
    }
}
