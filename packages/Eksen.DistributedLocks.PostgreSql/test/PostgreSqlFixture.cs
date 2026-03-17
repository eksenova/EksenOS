using Npgsql;
using Testcontainers.PostgreSql;

namespace Eksen.DistributedLocks.PostgreSql.Tests;

[CollectionDefinition(Name)]
public class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSql";
}

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder(image: "postgres:17-alpine")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
