using Eksen.TestBase;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Eksen.EntityFrameworkCore.Tests;

internal class SoftDeleteTestDbContext(DbContextOptions<SoftDeleteTestDbContext> options)
    : EksenDbContext(options)
{
    public DbSet<TestEntity> TestEntities => Set<TestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new TestEntityTypeConfiguration());
        modelBuilder.ApplyEksenSoftDeleteQueryFilter();
    }
}

public class ModelBuilderExtensionsTests : EksenUnitTestBase, IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly SoftDeleteTestDbContext _dbContext;

    public ModelBuilderExtensionsTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SoftDeleteTestDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AutoPropertiesSaveChangesInterceptor())
            .Options;

        _dbContext = new SoftDeleteTestDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void ApplyEksenSoftDeleteQueryFilter_Should_Filter_Soft_Deleted_Entities()
    {
        // Arrange
        var entity1 = new TestEntity(new TestEntityName("Active"));
        var entity2 = new TestEntity(new TestEntityName("ToDelete"));

        _dbContext.TestEntities.Add(entity1);
        _dbContext.TestEntities.Add(entity2);
        _dbContext.SaveChanges();

        _dbContext.TestEntities.Remove(entity2);
        _dbContext.SaveChanges();

        _dbContext.ChangeTracker.Clear();

        // Act
        var entities = _dbContext.TestEntities.ToList();

        // Assert
        entities.Count.ShouldBe(1);
        entities[0].Name.ShouldBe(new TestEntityName("Active"));
    }

    [Fact]
    public void IgnoreQueryFilters_Should_Include_Soft_Deleted_Entities()
    {
        // Arrange
        var entity1 = new TestEntity(new TestEntityName("Active2"));
        var entity2 = new TestEntity(new TestEntityName("Deleted2"));

        _dbContext.TestEntities.Add(entity1);
        _dbContext.TestEntities.Add(entity2);
        _dbContext.SaveChanges();

        _dbContext.TestEntities.Remove(entity2);
        _dbContext.SaveChanges();

        _dbContext.ChangeTracker.Clear();

        // Act
        var entities = _dbContext.TestEntities.IgnoreQueryFilters().ToList();

        // Assert
        entities.Count.ShouldBe(2);
    }
}
