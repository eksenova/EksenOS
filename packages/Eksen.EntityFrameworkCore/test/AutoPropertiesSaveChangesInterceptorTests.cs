using Eksen.Entities;
using Eksen.TestBase;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Eksen.EntityFrameworkCore.Tests;

internal class TimestampedEntity : IHasCreationTime, IHasModificationTime
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreationTime { get; set; }
    public DateTime? LastModificationTime { get; set; }
}

internal class TimestampedDbContext(DbContextOptions<TimestampedDbContext> options) : DbContext(options)
{
    public DbSet<TimestampedEntity> TimestampedEntities => Set<TimestampedEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TimestampedEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).ValueGeneratedOnAdd();
        });
    }
}

public class AutoPropertiesSaveChangesInterceptorTests : EksenUnitTestBase, IDisposable
{
    private readonly TestDbContext _softDeleteDbContext;
    private readonly TimestampedDbContext _timestampDbContext;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _softDeleteConnection;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _timestampConnection;

    public AutoPropertiesSaveChangesInterceptorTests()
    {
        _softDeleteConnection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        _softDeleteConnection.Open();
        var softDeleteOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_softDeleteConnection)
            .AddInterceptors(new AutoPropertiesSaveChangesInterceptor())
            .Options;
        _softDeleteDbContext = new TestDbContext(softDeleteOptions);
        _softDeleteDbContext.Database.EnsureCreated();

        _timestampConnection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        _timestampConnection.Open();
        var timestampOptions = new DbContextOptionsBuilder<TimestampedDbContext>()
            .UseSqlite(_timestampConnection)
            .AddInterceptors(new AutoPropertiesSaveChangesInterceptor())
            .Options;
        _timestampDbContext = new TimestampedDbContext(timestampOptions);
        _timestampDbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _softDeleteDbContext.Dispose();
        _softDeleteConnection.Dispose();
        _timestampDbContext.Dispose();
        _timestampConnection.Dispose();
    }

    [Fact]
    public async Task SavingChangesAsync_Should_Set_CreationTime_On_Added_Entity()
    {
        // Arrange
        var entity = new TimestampedEntity { Name = "Test" };

        // Act
        _timestampDbContext.TimestampedEntities.Add(entity);
        await _timestampDbContext.SaveChangesAsync();

        // Assert
        entity.CreationTime.ShouldNotBe(default);
    }

    [Fact]
    public async Task SavingChangesAsync_Should_Not_Override_Existing_CreationTime()
    {
        // Arrange
        var existingTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var entity = new TimestampedEntity { Name = "Test", CreationTime = existingTime };

        // Act
        _timestampDbContext.TimestampedEntities.Add(entity);
        await _timestampDbContext.SaveChangesAsync();

        // Assert
        entity.CreationTime.ShouldBe(existingTime);
    }

    [Fact]
    public async Task SavingChangesAsync_Should_Set_LastModificationTime_On_Modified_Entity()
    {
        // Arrange
        var entity = new TimestampedEntity { Name = "Test" };
        _timestampDbContext.TimestampedEntities.Add(entity);
        await _timestampDbContext.SaveChangesAsync();
        entity.LastModificationTime = null; // reset after SavedChanges sets it

        // Act
        entity.Name = "Updated";
        _timestampDbContext.Entry(entity).State = EntityState.Modified;
        await _timestampDbContext.SaveChangesAsync();

        // Assert
        entity.LastModificationTime.ShouldNotBeNull();
    }

    [Fact]
    public async Task SavingChangesAsync_Should_Not_Override_Existing_LastModificationTime()
    {
        // Arrange
        var existingTime = new DateTime(2020, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var entity = new TimestampedEntity { Name = "Test" };
        _timestampDbContext.TimestampedEntities.Add(entity);
        await _timestampDbContext.SaveChangesAsync();

        // Act
        entity.Name = "Updated";
        entity.LastModificationTime = existingTime;
        _timestampDbContext.Entry(entity).State = EntityState.Modified;
        await _timestampDbContext.SaveChangesAsync();

        // Assert
        entity.LastModificationTime.ShouldBe(existingTime);
    }

    [Fact]
    public void SavingChanges_Should_Set_IsDeleted_On_Soft_Delete()
    {
        // Arrange
        var entity = new TestEntity(new TestEntityName("SoftDeleteTest"));
        _softDeleteDbContext.TestEntities.Add(entity);
        _softDeleteDbContext.SaveChanges();

        // Act
        _softDeleteDbContext.TestEntities.Remove(entity);
        _softDeleteDbContext.SaveChanges();

        // Assert - entity should still exist in database with IsDeleted=true
        _softDeleteDbContext.ChangeTracker.Clear();
        var loaded = _softDeleteDbContext.TestEntities.IgnoreQueryFilters().First(e => e.Id == entity.Id);
        loaded.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task SavingChangesAsync_Should_Set_IsDeleted_On_Soft_Delete()
    {
        // Arrange
        var entity = new TestEntity(new TestEntityName("SoftDeleteAsyncTest"));
        _softDeleteDbContext.TestEntities.Add(entity);
        await _softDeleteDbContext.SaveChangesAsync();

        // Act
        _softDeleteDbContext.TestEntities.Remove(entity);
        await _softDeleteDbContext.SaveChangesAsync();

        // Assert - entity should still exist in database with IsDeleted=true
        _softDeleteDbContext.ChangeTracker.Clear();
        var loaded = await _softDeleteDbContext.TestEntities.IgnoreQueryFilters().FirstAsync(e => e.Id == entity.Id);
        loaded.IsDeleted.ShouldBeTrue();
    }
}
