using System.Threading.Channels;
using Eksen.TestBase;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace Eksen.EntityFrameworkCore.SqlServer.Tests;

[CollectionDefinition(Name)]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly Channel<SqlServerWorker> _available;
    private readonly SemaphoreSlim _creationGate = new(initialCount: 1, maxCount: 1);
    private readonly List<SqlServerWorker> _workers = [];
    private int _created;

    internal int MaxWorkers { get; }

    public SqlServerFixture()
    {
        MaxWorkers =
            int.TryParse(Environment.GetEnvironmentVariable(variable: "EKSEN_SQL_MAX_WORKERS"), out var max) && max > 0
                ? max
                : 2;

        _available = Channel.CreateBounded<SqlServerWorker>(MaxWorkers);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var worker in _workers)
        {
            await worker.Container.DisposeAsync();
        }
    }

    internal async Task<SqlServerWorker> AcquireAsync(CancellationToken cancellationToken = default)
    {
        if (_available.Reader.TryRead(out var worker))
        {
            await worker.CleanAsync(cancellationToken);
            return worker;
        }

        await _creationGate.WaitAsync(cancellationToken);
        try
        {
            if (_available.Reader.TryRead(out worker))
            {
                await worker.CleanAsync(cancellationToken);
                return worker;
            }

            if (_created < MaxWorkers)
            {
                worker = await SqlServerWorker.CreateAsync(cancellationToken);
                _created++;
                _workers.Add(worker);
                return worker;
            }
        }
        finally
        {
            _creationGate.Release();
        }

        worker = await _available.Reader.ReadAsync(cancellationToken);
        await worker.CleanAsync(cancellationToken);
        return worker;
    }

    internal async Task ReleaseAsync(SqlServerWorker worker)
    {
        await _available.Writer.WriteAsync(worker);
    }
}

[Collection(SqlServerCollection.Name)]
public abstract class EksenSqlServerTestBase(SqlServerFixture fixture)
    : EksenDatabaseTestBase<TestDbContext>
{
    private SqlServerWorker? _worker;

    protected string ConnectionString
    {
        get
        {
            return _worker?.ConnectionString
                   ?? throw new InvalidOperationException(message: "No SQL Server worker has been acquired yet.");
        }
    }

    protected override async Task<string> GetConnectionStringAsync()
    {
        _worker = await fixture.AcquireAsync();
        return _worker.ConnectionString;
    }

    protected override void ConfigureDbContext(
        IEksenEntityFrameworkCoreBuilder efCoreBuilder,
        string connectionString)
    {
        efCoreBuilder.UseSqlServerDbContext<TestDbContext>(connectionString);
    }

    public override async Task DisposeAsync()
    {
        if (_worker is not null)
        {
            await fixture.ReleaseAsync(_worker);
            _worker = null;
        }

        await base.DisposeAsync();
    }
}

internal sealed class SqlServerWorker
{
    private const string DatabaseName = "EksenTestDb";

    internal MsSqlContainer Container { get; }

    internal string ConnectionString { get; }

    private SqlServerWorker(MsSqlContainer container, string connectionString)
    {
        Container = container;
        ConnectionString = connectionString;
    }

    internal static async Task<SqlServerWorker> CreateAsync(CancellationToken cancellationToken = default)
    {
        var container = new MsSqlBuilder(image: "mcr.microsoft.com/mssql/server:2025-latest")
            .WithPassword(password: "123*!%AbC")
            .Build();

        await container.StartAsync(cancellationToken);

        await using (var connection = new SqlConnection(container.GetConnectionString()))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(
                $"CREATE DATABASE [{DatabaseName}]", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var builder = new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = DatabaseName
        };

        return new SqlServerWorker(container, builder.ConnectionString);
    }

    internal async Task CleanAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            cmdText: """
                     -- Drop every foreign-key constraint first to avoid dependency errors
                     DECLARE @fkSql NVARCHAR(MAX) = N'';
                     SELECT @fkSql += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
                         + N' DROP CONSTRAINT ' + QUOTENAME(f.name) + N';'
                     FROM sys.foreign_keys f
                     INNER JOIN sys.tables t ON f.parent_object_id = t.object_id
                     INNER JOIN sys.schemas s ON t.schema_id = s.schema_id;
                     EXEC sp_executesql @fkSql;

                     -- Then drop every user table
                     DECLARE @tblSql NVARCHAR(MAX) = N'';
                     SELECT @tblSql += N'DROP TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';'
                     FROM sys.tables t
                     INNER JOIN sys.schemas s ON t.schema_id = s.schema_id;
                     EXEC sp_executesql @tblSql;
                     """, connection);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}