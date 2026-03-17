using Eksen.Identity.Roles;
using Eksen.Identity.Users;
using Eksen.Permissions.EntityFrameworkCore.Tests.Fakes;
using Eksen.TestBase;
using Eksen.ValueObjects.Emailing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Eksen.Permissions.EntityFrameworkCore.Tests;

public class EfCoreUserRoleRepositoryTests : EksenUnitTestBase, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PermissionsTestDbContext _dbContext;
    private readonly EfCoreEksenUserRoleRepository<PermissionsTestDbContext, TestUser, TestRole, TestTenant> _sut;

    public EfCoreUserRoleRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PermissionsTestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new PermissionsTestDbContext(options);
        _dbContext.Database.EnsureCreated();
        _sut = new EfCoreEksenUserRoleRepository<PermissionsTestDbContext, TestUser, TestRole, TestTenant>(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetRolesByUserIdAsync_Should_Return_Roles_For_User()
    {
        // Arrange
        var user = new TestUser { EmailAddress = EmailAddress.Parse("user@test.com") };
        _dbContext.Users.Add(user);

        var role = new TestRole { Name = RoleName.Create("Admin") };
        _dbContext.Roles.Add(role);

        _dbContext.UserRoles.Add(new EksenUserRole<TestUser, TestRole, TestTenant>(user, role, null));
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetRolesByUserIdAsync(user.Id);

        // Assert
        result.Count.ShouldBe(1);
        result.First().Name.Value.ShouldBe("Admin");
    }

    [Fact]
    public async Task GetRolesByUserIdAsync_Should_Return_Multiple_Roles()
    {
        // Arrange
        var user = new TestUser { EmailAddress = EmailAddress.Parse("multi@test.com") };
        _dbContext.Users.Add(user);

        var role1 = new TestRole { Name = RoleName.Create("Admin") };
        var role2 = new TestRole { Name = RoleName.Create("Editor") };
        _dbContext.Roles.AddRange(role1, role2);

        _dbContext.UserRoles.Add(new EksenUserRole<TestUser, TestRole, TestTenant>(user, role1, null));
        _dbContext.UserRoles.Add(new EksenUserRole<TestUser, TestRole, TestTenant>(user, role2, null));
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetRolesByUserIdAsync(user.Id);

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetRolesByUserIdAsync_Should_Return_Empty_When_No_Roles()
    {
        // Arrange
        var user = new TestUser { EmailAddress = EmailAddress.Parse("noroles@test.com") };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetRolesByUserIdAsync(user.Id);

        // Assert
        result.ShouldBeEmpty();
    }
}
