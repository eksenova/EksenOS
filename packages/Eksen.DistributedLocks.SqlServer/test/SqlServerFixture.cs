using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Eksen.DistributedLocks.SqlServer.Tests;

[CollectionDefinition(Name)]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}

public sealed class SqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer _container = null!;

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder(image: "mcr.microsoft.com/mssql/server:2025-latest")
            .WithPassword("123*!%AbC")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
