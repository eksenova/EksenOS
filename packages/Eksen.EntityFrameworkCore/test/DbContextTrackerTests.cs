using Eksen.TestBase;
using Eksen.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Moq;
using Shouldly;

namespace Eksen.EntityFrameworkCore.Tests;

public class DbContextTrackerTests : EksenUnitTestBase
{
    private static IUnitOfWorkScope CreateScope() => Mock.Of<IUnitOfWorkScope>();

    private static DbContext CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseSqlite(connection)
            .Options;
        return new DbContext(options);
    }

    [Fact]
    public void TrackDbContext_Should_Track_Context_For_Scope()
    {
        // Arrange
        var tracker = new DbContextTracker();
        var scope = CreateScope();
        var dbContext = CreateDbContext();

        // Act
        tracker.TrackDbContext(scope, dbContext);

        // Assert
        var contexts = tracker.GetScopeDbContexts(scope);
        contexts.Count.ShouldBe(1);
        contexts.ShouldContain(dbContext);
    }

    [Fact]
    public void TrackDbContext_Should_Not_Duplicate_Same_Context()
    {
        // Arrange
        var tracker = new DbContextTracker();
        var scope = CreateScope();
        var dbContext = CreateDbContext();

        // Act
        tracker.TrackDbContext(scope, dbContext);
        tracker.TrackDbContext(scope, dbContext);

        // Assert
        var contexts = tracker.GetScopeDbContexts(scope);
        contexts.Count.ShouldBe(1);
    }

    [Fact]
    public void TrackDbContext_Should_Track_Multiple_Contexts_For_Same_Scope()
    {
        // Arrange
        var tracker = new DbContextTracker();
        var scope = CreateScope();
        var dbContext1 = CreateDbContext();
        var dbContext2 = CreateDbContext();

        // Act
        tracker.TrackDbContext(scope, dbContext1);
        tracker.TrackDbContext(scope, dbContext2);

        // Assert
        var contexts = tracker.GetScopeDbContexts(scope);
        contexts.Count.ShouldBe(2);
        contexts.ShouldContain(dbContext1);
        contexts.ShouldContain(dbContext2);
    }

    [Fact]
    public void GetScopeDbContexts_Should_Return_Empty_When_Scope_Not_Tracked()
    {
        // Arrange
        var tracker = new DbContextTracker();
        var scope = CreateScope();

        // Act
        var contexts = tracker.GetScopeDbContexts(scope);

        // Assert
        contexts.ShouldBeEmpty();
    }

    [Fact]
    public void ClearScope_Should_Remove_All_Contexts_For_Scope()
    {
        // Arrange
        var tracker = new DbContextTracker();
        var scope = CreateScope();
        var dbContext1 = CreateDbContext();
        var dbContext2 = CreateDbContext();

        tracker.TrackDbContext(scope, dbContext1);
        tracker.TrackDbContext(scope, dbContext2);

        // Act
        tracker.ClearScope(scope);

        // Assert
        var contexts = tracker.GetScopeDbContexts(scope);
        contexts.ShouldBeEmpty();
    }

    [Fact]
    public void ClearScope_Should_Not_Affect_Other_Scopes()
    {
        // Arrange
        var tracker = new DbContextTracker();
        var scope1 = CreateScope();
        var scope2 = CreateScope();
        var dbContext1 = CreateDbContext();
        var dbContext2 = CreateDbContext();

        tracker.TrackDbContext(scope1, dbContext1);
        tracker.TrackDbContext(scope2, dbContext2);

        // Act
        tracker.ClearScope(scope1);

        // Assert
        tracker.GetScopeDbContexts(scope1).ShouldBeEmpty();
        tracker.GetScopeDbContexts(scope2).Count.ShouldBe(1);
        tracker.GetScopeDbContexts(scope2).ShouldContain(dbContext2);
    }

    [Fact]
    public void ClearScope_Should_Not_Throw_When_Scope_Not_Tracked()
    {
        // Arrange
        var tracker = new DbContextTracker();
        var scope = CreateScope();

        // Act & Assert
        Should.NotThrow(() => tracker.ClearScope(scope));
    }
}
