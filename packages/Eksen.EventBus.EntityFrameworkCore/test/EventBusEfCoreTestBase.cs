using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Eksen.EventBus.EntityFrameworkCore.Tests;

public abstract class EventBusEfCoreTestBase : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    protected EventBusDbContext DbContext { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<EventBusDbContext>()
            .UseSqlite(_connection)
            .Options;

        DbContext = new EventBusDbContext(options);
        await DbContext.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
