using Eksen.Permissions.EntityFrameworkCore.Tests.Fakes;
using Eksen.TestBase;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Eksen.Permissions.EntityFrameworkCore.Tests;

public class EfCorePermissionDefinitionRepositoryTests : EksenUnitTestBase, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PermissionsTestDbContext _dbContext;
    private readonly EfCoreEksenPermissionDefinitionRepository<PermissionsTestDbContext> _sut;

    public EfCorePermissionDefinitionRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PermissionsTestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new PermissionsTestDbContext(options);
        _dbContext.Database.EnsureCreated();
        _sut = new EfCoreEksenPermissionDefinitionRepository<PermissionsTestDbContext>(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private async Task<PermissionDefinition> SeedDefinitionAsync(string name, bool isDisabled = false)
    {
        var def = new PermissionDefinition(PermissionName.Create(name));
        if (isDisabled) def.SetIsEnabled(false);
        _dbContext.PermissionDefinitions.Add(def);
        await _dbContext.SaveChangesAsync();
        return def;
    }

    [Fact]
    public async Task InsertAsync_Should_Persist_PermissionDefinition()
    {
        // Arrange
        var def = new PermissionDefinition(PermissionName.Create("Orders.Create"));

        // Act
        await _sut.InsertAsync(def, autoSave: true);

        // Assert
        var found = await _dbContext.PermissionDefinitions.FindAsync(def.Id);
        found.ShouldNotBeNull();
        found.Name.Value.ShouldBe("Orders.Create");
    }

    [Fact]
    public async Task FindAsync_Should_Return_PermissionDefinition_By_Id()
    {
        // Arrange
        var def = await SeedDefinitionAsync("Orders.Create");

        // Act
        var found = await _sut.FindAsync(def.Id);

        // Assert
        found.ShouldNotBeNull();
        found.Name.Value.ShouldBe("Orders.Create");
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_IsDisabled()
    {
        // Arrange
        await SeedDefinitionAsync("Active.Permission");
        await SeedDefinitionAsync("Disabled.Permission", isDisabled: true);

        // Act
        var activeOnly = await _sut.GetListAsync(new PermissionFilterParameters { IsDisabled = false });

        // Assert
        activeOnly.Count.ShouldBe(1);
        activeOnly.First().Name.Value.ShouldBe("Active.Permission");
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_SearchFilter()
    {
        // Arrange
        await SeedDefinitionAsync("Orders.Create");
        await SeedDefinitionAsync("Users.Manage");

        // Act
        var result = await _sut.GetListAsync(new PermissionFilterParameters { SearchFilter = "Orders" });

        // Assert
        result.Count.ShouldBe(1);
        result.First().Name.Value.ShouldBe("Orders.Create");
    }

    [Fact]
    public async Task GetListAsync_Should_Return_All_When_No_Filter()
    {
        // Arrange
        await SeedDefinitionAsync("Orders.Create");
        await SeedDefinitionAsync("Users.Manage");

        // Act
        var result = await _sut.GetListAsync();

        // Assert
        result.Count.ShouldBe(2);
    }
}
