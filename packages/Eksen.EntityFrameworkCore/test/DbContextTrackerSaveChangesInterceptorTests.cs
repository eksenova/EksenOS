using Eksen.UnitOfWork;
using Eksen.TestBase;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Eksen.EntityFrameworkCore.Tests;

public class EfCoreUnitOfWorkScopeTests : EksenUnitTestBase, IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly TestDbContext _dbContext;
    private readonly DbContextTracker _tracker;

    public EfCoreUnitOfWorkScopeTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new TestDbContext(options);
        _dbContext.Database.EnsureCreated();

        _tracker = new DbContextTracker();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CommitAsync_Should_SaveChanges_Without_Transaction_When_Non_Transactional()
    {
        // Arrange
        var parentScope = new Mock<IUnitOfWorkScope>().Object;
        var provider = new EfCoreUnitOfWorkProvider(_tracker);
        var scope = new EfCoreUnitOfWorkScope(provider, parentScope, isTransactional: false, _tracker, isolationLevel: null);

        _tracker.TrackDbContext(parentScope, _dbContext);

        var entity = new TestEntity(new TestEntityName("CommitTest"));
        _dbContext.TestEntities.Add(entity);

        // Act
        await scope.CommitAsync();

        // Assert
        _dbContext.ChangeTracker.Clear();
        var entities = _dbContext.TestEntities.ToList();
        entities.Count.ShouldBe(1);
        entities[0].Name.ShouldBe(new TestEntityName("CommitTest"));
    }

    [Fact]
    public async Task CommitAsync_Should_Throw_When_Already_Committed()
    {
        // Arrange
        var parentScope = new Mock<IUnitOfWorkScope>().Object;
        var provider = new EfCoreUnitOfWorkProvider(_tracker);
        var scope = new EfCoreUnitOfWorkScope(provider, parentScope, isTransactional: false, _tracker, isolationLevel: null);

        _tracker.TrackDbContext(parentScope, _dbContext);

        await scope.CommitAsync();

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => scope.CommitAsync());

        exception.Message.ShouldContain("already been committed");
    }

    [Fact]
    public async Task RollbackAsync_Should_Throw_When_Already_Committed()
    {
        // Arrange
        var parentScope = new Mock<IUnitOfWorkScope>().Object;
        var provider = new EfCoreUnitOfWorkProvider(_tracker);
        var scope = new EfCoreUnitOfWorkScope(provider, parentScope, isTransactional: false, _tracker, isolationLevel: null);

        _tracker.TrackDbContext(parentScope, _dbContext);

        await scope.CommitAsync();

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => scope.RollbackAsync());

        exception.Message.ShouldContain("Cannot rollback a committed unit of work");
    }

    [Fact]
    public async Task SaveChangesAsync_Should_Persist_Without_Committing()
    {
        // Arrange
        var parentScope = new Mock<IUnitOfWorkScope>().Object;
        var provider = new EfCoreUnitOfWorkProvider(_tracker);
        var scope = new EfCoreUnitOfWorkScope(provider, parentScope, isTransactional: false, _tracker, isolationLevel: null);

        _tracker.TrackDbContext(parentScope, _dbContext);

        var entity = new TestEntity(new TestEntityName("SaveTest"));
        _dbContext.TestEntities.Add(entity);

        // Act
        await scope.SaveChangesAsync();

        // Assert
        _dbContext.ChangeTracker.Clear();
        var entities = _dbContext.TestEntities.ToList();
        entities.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_Should_Commit_If_Not_Already_Committed()
    {
        // Arrange
        var parentScope = new Mock<IUnitOfWorkScope>().Object;
        var provider = new EfCoreUnitOfWorkProvider(_tracker);
        var scope = new EfCoreUnitOfWorkScope(provider, parentScope, isTransactional: false, _tracker, isolationLevel: null);

        _tracker.TrackDbContext(parentScope, _dbContext);

        var entity = new TestEntity(new TestEntityName("DisposeTest"));
        _dbContext.TestEntities.Add(entity);

        // Act
        await scope.DisposeAsync();

        // Assert
        _dbContext.ChangeTracker.Clear();
        var entities = _dbContext.TestEntities.ToList();
        entities.Count.ShouldBe(1);
    }
}
