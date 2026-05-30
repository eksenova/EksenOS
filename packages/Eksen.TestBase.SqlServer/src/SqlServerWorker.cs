using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Eksen.TestBase.SqlServer;

/// <summary>
/// A single reusable SQL Server container together with a dedicated test database.
/// A worker is created once, then reused across many tests: each test that acquires the
/// worker gets a freshly cleaned (schema-less) database via <see cref="CleanAsync"/>.
/// </summary>
/// <remarks>
/// SQL Server 2025 asserts on start-up when it is given an odd number of logical processors
/// (a NUMA topology check), so every container pins an even CPU set through
/// <see cref="SqlServerTestEnvironment.CpuSet"/>.
/// </remarks>
internal sealed class SqlServerWorker
{
    private const string DatabaseName = "EksenTestDb";
    private const string SaPassword = "123*!%AbC";

    private SqlServerWorker(MsSqlContainer container, string connectionString)
    {
        Container = container;
        ConnectionString = connectionString;
    }

    internal MsSqlContainer Container { get; }

    internal string ConnectionString { get; }

    internal static async Task<SqlServerWorker> CreateAsync(CancellationToken cancellationToken = default)
    {
        var cpuSet = SqlServerTestEnvironment.CpuSet;

        var container = new MsSqlBuilder(image: "mcr.microsoft.com/mssql/server:2025-latest")
            .WithPassword(SaPassword)
            .WithCreateParameterModifier(parameters => parameters.HostConfig.CpusetCpus = cpuSet)
            .Build();

        await container.StartAsync(cancellationToken);

        await using (var connection = new SqlConnection(container.GetConnectionString()))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand($"CREATE DATABASE [{DatabaseName}]", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var builder = new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = DatabaseName
        };

        return new SqlServerWorker(container, builder.ConnectionString);
    }

    /// <summary>
    /// Drops every foreign key and then every user table so the next test starts from an
    /// empty schema. The provider's <c>EnsureCreated</c> rebuilds the schema afterwards.
    /// </summary>
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

    internal ValueTask DisposeContainerAsync()
    {
        return Container.DisposeAsync();
    }
}
