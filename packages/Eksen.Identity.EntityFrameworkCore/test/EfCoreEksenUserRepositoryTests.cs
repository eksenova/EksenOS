using Eksen.Identity.EntityFrameworkCore.Tests.Fakes;
using Eksen.Identity.EntityFrameworkCore.Users;
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Eksen.TestBase;
using Eksen.ValueObjects.Emailing;
using Eksen.ValueObjects.Hashing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Eksen.Identity.EntityFrameworkCore.Tests;

public class EfCoreEksenUserRepositoryTests : EksenUnitTestBase, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IdentityTestDbContext _dbContext;
    private readonly EfCoreEksenUserRepository<IdentityTestDbContext, TestUser, TestTenant> _sut;

    public EfCoreEksenUserRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<IdentityTestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new IdentityTestDbContext(options);
        _dbContext.Database.EnsureCreated();
        _sut = new EfCoreEksenUserRepository<IdentityTestDbContext, TestUser, TestTenant>(_dbContext);
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

    private async Task<TestUser> SeedUserAsync(
        string email = "test@example.com",
        bool isActive = true,
        TestTenant? tenant = null)
    {
        var user = new TestUser
        {
            EmailAddress = EmailAddress.Parse(email),
            IsActive = isActive,
            Tenant = tenant
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task InsertAsync_Should_Persist_User()
    {
        // Arrange
        var user = new TestUser { EmailAddress = EmailAddress.Parse("new@test.com") };

        // Act
        await _sut.InsertAsync(user, autoSave: true);

        // Assert
        var found = await _dbContext.Users.FindAsync(user.Id);
        found.ShouldNotBeNull();
        found.EmailAddress!.Value.ShouldBe("new@test.com");
    }

    [Fact]
    public async Task FindAsync_Should_Return_User_By_Id()
    {
        // Arrange
        var user = await SeedUserAsync();

        // Act
        var found = await _sut.FindAsync(user.Id);

        // Assert
        found.ShouldNotBeNull();
    }

    [Fact]
    public async Task FindAsync_Should_Return_Null_When_Not_Found()
    {
        // Act
        var found = await _sut.FindAsync(new EksenUserId(System.Ulid.NewUlid()));

        // Assert
        found.ShouldBeNull();
    }

    [Fact]
    public async Task FindByEmailAddressAsync_Should_Return_User()
    {
        // Arrange
        await SeedUserAsync("john@example.com");

        // Act
        var found = await _sut.FindByEmailAddressAsync(EmailAddress.Parse("john@example.com"));

        // Assert
        found.ShouldNotBeNull();
        found.EmailAddress!.Value.ShouldBe("john@example.com");
    }

    [Fact]
    public async Task FindByEmailAddressAsync_Should_Return_Null_When_Not_Found()
    {
        // Act
        var found = await _sut.FindByEmailAddressAsync(EmailAddress.Parse("missing@example.com"));

        // Assert
        found.ShouldBeNull();
    }

    [Fact]
    public async Task FindByIdAsync_Should_Return_User_With_Includes()
    {
        // Arrange
        var tenant = await SeedTenantAsync();
        var user = await SeedUserAsync("test@example.com", tenant: tenant);
        _dbContext.ChangeTracker.Clear();

        // Act
        var found = await _sut.FindByIdAsync(
            user.Id,
            includeOptions: new EksenUserIncludeOptions<TestUser, TestTenant> { IncludeTenant = true });

        // Assert
        found.ShouldNotBeNull();
        found.Tenant.ShouldNotBeNull();
    }

    [Fact]
    public async Task FindByIdAsync_Should_Return_Null_For_Null_Id()
    {
        // Act
        var found = await _sut.FindByIdAsync(userId: null);

        // Assert
        found.ShouldBeNull();
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_SearchFilter()
    {
        // Arrange
        await SeedUserAsync("john@example.com");
        await SeedUserAsync("jane@example.com");
        await SeedUserAsync("bob@other.com");

        // Act
        var results = await _sut.GetListAsync(
            filterParameters: new EksenUserFilterParameters<TestUser, TestTenant>
            {
                SearchFilter = "example"
            });

        // Assert
        results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_IsActive()
    {
        // Arrange
        await SeedUserAsync("active@test.com", isActive: true);
        await SeedUserAsync("inactive@test.com", isActive: false);

        // Act
        var results = await _sut.GetListAsync(
            filterParameters: new EksenUserFilterParameters<TestUser, TestTenant>
            {
                IsActive = true
            });

        // Assert
        results.Count.ShouldBe(1);
        results.First().EmailAddress!.Value.ShouldBe("active@test.com");
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_TenantId()
    {
        // Arrange
        var tenant1 = await SeedTenantAsync("Tenant A");
        var tenant2 = await SeedTenantAsync("Tenant B");
        await SeedUserAsync("user1@test.com", tenant: tenant1);
        await SeedUserAsync("user2@test.com", tenant: tenant2);

        // Act
        var results = await _sut.GetListAsync(
            filterParameters: new EksenUserFilterParameters<TestUser, TestTenant>
            {
                TenantId = tenant1.Id
            });

        // Assert
        results.Count.ShouldBe(1);
        results.First().EmailAddress!.Value.ShouldBe("user1@test.com");
    }

    [Fact]
    public async Task GetListAsync_Should_Return_All_When_No_Filters()
    {
        // Arrange
        await SeedUserAsync("user1@test.com");
        await SeedUserAsync("user2@test.com");

        // Act
        var results = await _sut.GetListAsync();

        // Assert
        results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task FindAsync_Should_Include_Tenant_When_Requested()
    {
        // Arrange
        var tenant = await SeedTenantAsync();
        var user = await SeedUserAsync("test@test.com", tenant: tenant);
        _dbContext.ChangeTracker.Clear();

        // Act
        var found = await _sut.FindAsync(
            user.Id,
            includeOptions: new EksenUserIncludeOptions<TestUser, TestTenant> { IncludeTenant = true });

        // Assert
        found.ShouldNotBeNull();
        found.Tenant.ShouldNotBeNull();
        found.Tenant!.Name.Value.ShouldBe("Test Tenant");
    }

    [Fact]
    public async Task UpdateAsync_Should_Persist_Changes()
    {
        // Arrange
        var user = await SeedUserAsync();

        // Act
        user.SetActive(false);
        await _sut.UpdateAsync(user, autoSave: true);
        _dbContext.ChangeTracker.Clear();

        // Assert
        var found = await _dbContext.Users.FindAsync(user.Id);
        found.ShouldNotBeNull();
        found.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveAsync_Should_Delete_User()
    {
        // Arrange
        var user = await SeedUserAsync();

        // Act
        await _sut.RemoveAsync(user, autoSave: true);

        // Assert
        var found = await _dbContext.Users.FindAsync(user.Id);
        found.ShouldBeNull();
    }

    [Fact]
    public async Task User_Should_Persist_PasswordHash()
    {
        // Arrange
        var user = new TestUser
        {
            EmailAddress = EmailAddress.Parse("hash@test.com"),
            PasswordHash = PasswordHash.Create("$2a$10$hashedvalue")
        };

        // Act
        await _sut.InsertAsync(user, autoSave: true);
        _dbContext.ChangeTracker.Clear();

        // Assert
        var found = await _dbContext.Users.FindAsync(user.Id);
        found.ShouldNotBeNull();
        found.PasswordHash.ShouldNotBeNull();
        found.PasswordHash!.Value.ShouldBe("$2a$10$hashedvalue");
    }
}
