using Eksen.Identity.EntityFrameworkCore.Roles;
using Eksen.Identity.EntityFrameworkCore.Tests.Fakes;
using Eksen.Identity.Roles;
using Eksen.Identity.Tenants;
using Eksen.TestBase;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Eksen.Identity.EntityFrameworkCore.Tests;

public class EfCoreEksenRoleRepositoryTests : EksenUnitTestBase, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IdentityTestDbContext _dbContext;
    private readonly EfCoreEksenRoleRepository<IdentityTestDbContext, TestRole, TestTenant> _sut;

    public EfCoreEksenRoleRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<IdentityTestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new IdentityTestDbContext(options);
        _dbContext.Database.EnsureCreated();
        _sut = new EfCoreEksenRoleRepository<IdentityTestDbContext, TestRole, TestTenant>(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private async Task<TestTenant> SeedTenantAsync(string name = "Test Tenant")
    {
        var tenant = new TestTenant { Name = TenantName.Create(name) };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();
        return tenant;
    }

    private async Task<TestRole> SeedRoleAsync(string name = "Admin", TestTenant? tenant = null)
    {
        var role = new TestRole { Name = RoleName.Create(name), Tenant = tenant };
        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync();
        return role;
    }

    [Fact]
    public async Task InsertAsync_Should_Persist_Role()
    {
        // Arrange
        var role = new TestRole { Name = RoleName.Create("NewRole") };

        // Act
        await _sut.InsertAsync(role, autoSave: true);

        // Assert
        var found = await _dbContext.Roles.FindAsync(role.Id);
        found.ShouldNotBeNull();
        found.Name.Value.ShouldBe("NewRole");
    }

    [Fact]
    public async Task FindAsync_Should_Return_Role_By_Id()
    {
        // Arrange
        var role = await SeedRoleAsync("Admin");

        // Act
        var found = await _sut.FindAsync(role.Id);

        // Assert
        found.ShouldNotBeNull();
        found.Name.Value.ShouldBe("Admin");
    }

    [Fact]
    public async Task FindAsync_Should_Return_Null_When_Not_Found()
    {
        // Act
        var found = await _sut.FindAsync(new EksenRoleId(System.Ulid.NewUlid()));

        // Assert
        found.ShouldBeNull();
    }

    [Fact]
    public async Task FindAsync_Should_Include_Tenant_When_Requested()
    {
        // Arrange
        var tenant = await SeedTenantAsync();
        var role = await SeedRoleAsync("Admin", tenant);
        _dbContext.ChangeTracker.Clear();

        // Act
        var found = await _sut.FindAsync(
            role.Id,
            includeOptions: new EksenRoleIncludeOptions<TestRole, TestTenant> { IncludeTenant = true });

        // Assert
        found.ShouldNotBeNull();
        found.Tenant.ShouldNotBeNull();
        found.Tenant!.Name.Value.ShouldBe("Test Tenant");
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_SearchFilter()
    {
        // Arrange
        await SeedRoleAsync("Admin");
        await SeedRoleAsync("Editor");
        await SeedRoleAsync("Viewer");

        // Act
        var results = await _sut.GetListAsync(
            filterParameters: new EksenRoleFilterParameters<TestRole, TestTenant> { SearchFilter = "min" });

        // Assert
        results.Count.ShouldBe(1);
        results.First().Name.Value.ShouldBe("Admin");
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_Name()
    {
        // Arrange
        await SeedRoleAsync("Admin");
        await SeedRoleAsync("Editor");

        // Act
        var results = await _sut.GetListAsync(
            filterParameters: new EksenRoleFilterParameters<TestRole, TestTenant>
            {
                Name = RoleName.Create("Editor")
            });

        // Assert
        results.Count.ShouldBe(1);
        results.First().Name.Value.ShouldBe("Editor");
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_TenantId()
    {
        // Arrange
        var tenant1 = await SeedTenantAsync("Tenant A");
        var tenant2 = await SeedTenantAsync("Tenant B");
        await SeedRoleAsync("Admin", tenant1);
        await SeedRoleAsync("Editor", tenant2);

        // Act
        var results = await _sut.GetListAsync(
            filterParameters: new EksenRoleFilterParameters<TestRole, TestTenant>
            {
                TenantId = tenant1.Id
            });

        // Assert
        results.Count.ShouldBe(1);
        results.First().Name.Value.ShouldBe("Admin");
    }

    [Fact]
    public async Task GetListAsync_Should_Return_All_When_No_Filters()
    {
        // Arrange
        await SeedRoleAsync("Admin");
        await SeedRoleAsync("Editor");

        // Act
        var results = await _sut.GetListAsync();

        // Assert
        results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task UpdateAsync_Should_Persist_Changes()
    {
        // Arrange
        var role = await SeedRoleAsync("Admin");

        // Act
        role.SetName(RoleName.Create("SuperAdmin"));
        await _sut.UpdateAsync(role, autoSave: true);
        _dbContext.ChangeTracker.Clear();

        // Assert
        var found = await _dbContext.Roles.FindAsync(role.Id);
        found.ShouldNotBeNull();
        found.Name.Value.ShouldBe("SuperAdmin");
    }

    [Fact]
    public async Task RemoveAsync_Should_Delete_Role()
    {
        // Arrange
        var role = await SeedRoleAsync("ToDelete");

        // Act
        await _sut.RemoveAsync(role, autoSave: true);

        // Assert
        var found = await _dbContext.Roles.FindAsync(role.Id);
        found.ShouldBeNull();
    }
}
