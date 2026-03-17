using Eksen.Identity.Users;
using Eksen.Permissions.EntityFrameworkCore.Tests.Fakes;
using Eksen.TestBase;
using Eksen.ValueObjects.Emailing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Eksen.Permissions.EntityFrameworkCore.Tests;

public class EfCoreUserPermissionRepositoryTests : EksenUnitTestBase, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PermissionsTestDbContext _dbContext;
    private readonly EfCoreEksenUserPermissionRepository<PermissionsTestDbContext, TestUser, TestTenant> _sut;

    public EfCoreUserPermissionRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PermissionsTestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new PermissionsTestDbContext(options);
        _dbContext.Database.EnsureCreated();
        _sut = new EfCoreEksenUserPermissionRepository<PermissionsTestDbContext, TestUser, TestTenant>(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private async Task<(TestUser User, PermissionDefinition Def)> SeedUserPermissionAsync(
        string permName, string email = "user@test.com")
    {
        var user = new TestUser { EmailAddress = EmailAddress.Parse(email) };
        _dbContext.Users.Add(user);

        var def = new PermissionDefinition(PermissionName.Create(permName));
        _dbContext.PermissionDefinitions.Add(def);

        var up = new EksenUserPermission<TestUser, TestTenant>(user, def, null);
        _dbContext.UserPermissions.Add(up);

        await _dbContext.SaveChangesAsync();
        return (user, def);
    }

    [Fact]
    public async Task GetByUserIdAsync_Should_Return_Permissions_For_User()
    {
        // Arrange
        var (user, _) = await SeedUserPermissionAsync("Orders.Create");

        // Act
        var result = await _sut.GetByUserIdAsync(user.Id);

        // Assert
        result.Count.ShouldBe(1);
        result.First().Name.Value.ShouldBe("Orders.Create");
    }

    [Fact]
    public async Task GetByUserIdAsync_Should_Return_Multiple_Permissions()
    {
        // Arrange
        var user = new TestUser { EmailAddress = EmailAddress.Parse("multi@test.com") };
        _dbContext.Users.Add(user);

        var def1 = new PermissionDefinition(PermissionName.Create("Orders.Create"));
        var def2 = new PermissionDefinition(PermissionName.Create("Users.Manage"));
        _dbContext.PermissionDefinitions.AddRange(def1, def2);

        _dbContext.UserPermissions.Add(new EksenUserPermission<TestUser, TestTenant>(user, def1, null));
        _dbContext.UserPermissions.Add(new EksenUserPermission<TestUser, TestTenant>(user, def2, null));

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetByUserIdAsync(user.Id);

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetByUserIdAsync_Should_Return_Empty_When_No_Permissions()
    {
        // Arrange
        var user = new TestUser { EmailAddress = EmailAddress.Parse("empty@test.com") };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetByUserIdAsync(user.Id);

        // Assert
        result.ShouldBeEmpty();
    }
}
