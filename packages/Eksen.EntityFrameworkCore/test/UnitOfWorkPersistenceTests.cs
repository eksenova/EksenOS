using Eksen.TestBase;
using Eksen.UnitOfWork;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Eksen.EntityFrameworkCore.Tests;

public sealed class UnitOfWorkPersistenceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private ServiceProvider _services = null!;

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddEksen(eksen => eksen.AddUnitOfWork().AddEntityFrameworkCore());
        services.AddDbContext<TestDbContext>((provider, options) =>
        {
            var manager = provider.GetRequiredService<IUnitOfWorkManager>();
            var tracker = provider.GetRequiredService<IDbContextTracker>();
            options.UseSqlite(_connection).AddInterceptors(
                new DbContextTrackerCommandInterceptor(manager, tracker),
                new DbContextTrackerSaveChangesInterceptor(manager, tracker));
        });
        services.AddScoped<TestEntityRepository>();
        _services = services.BuildServiceProvider();
        await using var serviceScope = _services.CreateAsyncScope();
        await serviceScope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task InsertionOnlyScopeCommitsRepositoryChanges()
    {
        await using var services = _services.CreateAsyncScope();
        var provider = services.ServiceProvider;
        var manager = provider.GetRequiredService<IUnitOfWorkManager>();
        await using var scope = manager.BeginScope(isolationLevel: System.Data.IsolationLevel.Serializable);
        await provider.GetRequiredService<TestEntityRepository>().InsertAsync(new TestEntity(new TestEntityName("insert")));

        await scope.CommitAsync();

        Assert.Null(manager.Current);
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task RollbackDoesNotFlushPendingChangesDuringDisposal()
    {
        await using var services = _services.CreateAsyncScope();
        var provider = services.ServiceProvider;
        var manager = provider.GetRequiredService<IUnitOfWorkManager>();
        await using var scope = manager.BeginScope(isolationLevel: System.Data.IsolationLevel.Serializable);
        var context = provider.GetRequiredService<TestDbContext>();
        await context.TestEntities.CountAsync();
        await provider.GetRequiredService<TestEntityRepository>().InsertAsync(new TestEntity(new TestEntityName("rollback")));

        await scope.RollbackAsync();
        await scope.DisposeAsync();

        Assert.Null(manager.Current);
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task RepositoryAutoSaveRemainsInsideTransaction()
    {
        await using var services = _services.CreateAsyncScope();
        var provider = services.ServiceProvider;
        var manager = provider.GetRequiredService<IUnitOfWorkManager>();
        await using var scope = manager.BeginScope(isolationLevel: System.Data.IsolationLevel.Serializable);
        await provider.GetRequiredService<TestEntityRepository>().InsertAsync(
            new TestEntity(new TestEntityName("autosave")), autoSave: true);

        await scope.RollbackAsync();

        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task ExplicitFlushCanBeRolledBackWithoutLeavingAnActiveTransaction()
    {
        await using var services = _services.CreateAsyncScope();
        var provider = services.ServiceProvider;
        var manager = provider.GetRequiredService<IUnitOfWorkManager>();
        await using var scope = manager.BeginScope(isolationLevel: System.Data.IsolationLevel.Serializable);
        var context = provider.GetRequiredService<TestDbContext>();
        await context.TestEntities.CountAsync();
        context.TestEntities.Add(new TestEntity(new TestEntityName("flush")));
        await scope.SaveChangesAsync();

        await scope.RollbackAsync();

        Assert.Null(context.Database.CurrentTransaction);
        Assert.Null(manager.Current);
        Assert.Equal(0, await CountAsync());
    }

    private async Task<int> CountAsync()
    {
        await using var services = _services.CreateAsyncScope();
        return await services.ServiceProvider.GetRequiredService<TestDbContext>().TestEntities.CountAsync();
    }
}
