using Eksen.Identity.EntityFrameworkCore.Tenants;
using Eksen.Identity.EntityFrameworkCore.Tests.Fakes;
using Eksen.Identity.Tenants;
using Eksen.TestBase;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Eksen.Identity.EntityFrameworkCore.Tests;

public class EfCoreEksenTenantRepositoryTests : EksenUnitTestBase, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IdentityTestDbContext _dbContext;
    private readonly EfCoreEksenTenantRepository<IdentityTestDbContext, TestTenant> _sut;

    public EfCoreEksenTenantRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<IdentityTestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new IdentityTestDbContext(options);
        _dbContext.Database.EnsureCreated();
        _sut = new EfCoreEksenTenantRepository<IdentityTestDbContext, TestTenant>(_dbContext);
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

    [Fact]
    public async Task InsertAsync_Should_Persist_Tenant()
    {
        // Arrange
        var tenant = new TestTenant { Name = TenantName.Create("Acme Corp") };

        // Act
        await _sut.InsertAsync(tenant, autoSave: true);

        // Assert
        var found = await _dbContext.Tenants.FindAsync(tenant.Id);
        found.ShouldNotBeNull();
        found.Name.Value.ShouldBe("Acme Corp");
    }

    [Fact]
    public async Task FindAsync_Should_Return_Tenant_By_Id()
    {
        // Arrange
        var tenant = await SeedTenantAsync("Acme");

        // Act
        var found = await _sut.FindAsync(tenant.Id);

        // Assert
        found.ShouldNotBeNull();
        found.Name.Value.ShouldBe("Acme");
    }

    [Fact]
    public async Task FindAsync_Should_Return_Null_When_Not_Found()
    {
        // Act
        var found = await _sut.FindAsync(new EksenTenantId(System.Ulid.NewUlid()));

        // Assert
        found.ShouldBeNull();
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_SearchFilter()
    {
        // Arrange
        await SeedTenantAsync("Acme Corp");
        await SeedTenantAsync("Globex Inc");
        await SeedTenantAsync("Acme Labs");

        // Act
        var results = await _sut.GetListAsync(
            filterParameters: new EksenTenantFilterParameters<TestTenant> { SearchFilter = "Acme" });

        // Assert
        results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetListAsync_Should_Return_All_When_No_Filters()
    {
        // Arrange
        await SeedTenantAsync("Acme");
        await SeedTenantAsync("Globex");

        // Act
        var results = await _sut.GetListAsync();

        // Assert
        results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task UpdateAsync_Should_Persist_Changes()
    {
        // Arrange
        var tenant = await SeedTenantAsync("OldName");

        // Act
        tenant.Name = TenantName.Create("NewName");
        await _sut.UpdateAsync(tenant, autoSave: true);
        _dbContext.ChangeTracker.Clear();

        // Assert
        var found = await _dbContext.Tenants.FindAsync(tenant.Id);
        found.ShouldNotBeNull();
        found.Name.Value.ShouldBe("NewName");
    }

    [Fact]
    public async Task RemoveAsync_Should_Delete_Tenant()
    {
        // Arrange
        var tenant = await SeedTenantAsync("ToDelete");

        // Act
        await _sut.RemoveAsync(tenant, autoSave: true);

        // Assert
        var found = await _dbContext.Tenants.FindAsync(tenant.Id);
        found.ShouldBeNull();
    }
}
