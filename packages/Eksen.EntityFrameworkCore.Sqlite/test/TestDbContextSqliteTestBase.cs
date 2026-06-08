using Eksen.TestBase;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Eksen.EntityFrameworkCore.Sqlite.Tests;

public abstract class TestDbContextSqliteTestBase : EksenDatabaseTestBase<TestDbContext>
{
    private SqliteConnection? _keepAliveConnection;

    protected override async Task<string> GetConnectionStringAsync()
    {
        // ":memory:" gives each connection its own private database. Because the test base
        // resolves TestDbContext once per DI scope, every scope would otherwise see an empty
        // database. A uniquely named, shared-cache in-memory database is shared across all
        // connections that use the same name, and a single keep-alive connection kept open for
        // the test's lifetime prevents the database from being discarded between scopes.
        var connectionString =
            "DataSource=file:eksen-sqlite-tests-" + Guid.NewGuid().ToString("N") + "?mode=memory&cache=shared";

        _keepAliveConnection = new SqliteConnection(connectionString);
        await _keepAliveConnection.OpenAsync();

        return connectionString;
    }

    protected override void ConfigureDbContext(
        IEksenEntityFrameworkCoreBuilder efCoreBuilder,
        string connectionString)
    {
        efCoreBuilder.UseSqliteDbContext<TestDbContext>(connectionString);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_keepAliveConnection != null)
        {
            await _keepAliveConnection.DisposeAsync();
            _keepAliveConnection = null;
        }

        await base.DisposeAsync();
    }
}
