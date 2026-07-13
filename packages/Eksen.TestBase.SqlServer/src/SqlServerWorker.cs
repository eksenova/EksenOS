using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Eksen.TestBase.SqlServer;

/// <summary>
/// A single reusable SQL Server container together with a dedicated test database.
/// A worker is created once, then reused across many tests: each test that acquires the
/// worker gets a database whose data has been wiped via <see cref="CleanAsync"/> while the
/// schema is left intact, so the provider's <c>EnsureCreated</c> builds the schema only on
/// the first acquisition and is a cheap no-op thereafter.
/// </summary>
/// <remarks>
/// SQL Server 2025 asserts on start-up when it is given an odd number of logical processors
/// (a NUMA topology check), so containers running that engine pin an even CPU set through
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

    /// <summary>
    /// Whether the database still holds data from the previous test and must be wiped before the
    /// next one. A freshly created worker starts clean, a release-time sanitizer clears the flag,
    /// and a plain release sets it so the next acquisition wipes on the way in.
    /// </summary>
    internal bool RequiresCleaning { get; set; }

    internal static async Task<SqlServerWorker> CreateAsync(CancellationToken cancellationToken = default)
    {
        var cpuSet = SqlServerTestEnvironment.CpuSet;

        // The engine's readiness is verified host-side (see WaitUntilReadyAsync) rather than through the
        // module's default strategy, which execs sqlcmd inside the container. That tool is absent from the
        // arm64 Azure SQL Edge image, so the container is only waited on until it is running and the real
        // login-readiness probe is done from the host — a strategy that works across every supported image.
        var builder = new MsSqlBuilder()
            .WithImage(SqlServerTestEnvironment.Image)
            .WithPassword(SaPassword)
            .WithWaitStrategy(Wait.ForUnixContainer().AddCustomWaitStrategy(new ContainerRunningWaitStrategy()));

        if (!string.IsNullOrWhiteSpace(cpuSet))
        {
            builder = builder.WithCreateParameterModifier(parameters => parameters.HostConfig.CpusetCpus = cpuSet);
        }

        var container = builder.Build();

        await container.StartAsync(cancellationToken);

        var adminConnectionString = container.GetConnectionString();
        await WaitUntilReadyAsync(adminConnectionString, cancellationToken);

        await using (var connection = new SqlConnection(adminConnectionString))
        {
            await connection.OpenAsync(cancellationToken);

            // Delayed durability trades the per-commit log flush for throughput. Losing the tail of the
            // log on a crash is irrelevant for a throwaway test database, and per-test data isolation is
            // unaffected: commits remain fully consistent and visible to every connection.
            await using var command = new SqlCommand(
                $"""
                 CREATE DATABASE [{DatabaseName}];
                 ALTER DATABASE [{DatabaseName}] SET DELAYED_DURABILITY = FORCED;
                 """,
                connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var connectionStringBuilder = new SqlConnectionStringBuilder(adminConnectionString)
        {
            InitialCatalog = DatabaseName
        };

        return new SqlServerWorker(container, connectionStringBuilder.ConnectionString);
    }

    /// <summary>
    /// Polls the server with a real connection until it accepts logins and answers a trivial query. The
    /// listening port opens before the engine is ready for logins, so a successful <c>SELECT 1</c> is the
    /// signal that the container is usable.
    /// </summary>
    private static async Task WaitUntilReadyAsync(string connectionString, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddMinutes(3);

        while (true)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                await using var command = new SqlCommand(cmdText: "SELECT 1", connection);
                await command.ExecuteScalarAsync(cancellationToken);

                return;
            }
            catch (SqlException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    /// <summary>
    /// Wipes all data so the next test starts from empty tables while leaving the schema in place.
    /// Only tables that actually hold rows are touched: the truncate set is resolved from
    /// <c>sys.dm_db_partition_stats</c>, the foreign keys that reference a table in that set are dropped
    /// (SQL Server refuses to <c>TRUNCATE</c> a table referenced by a foreign key even when the constraint
    /// is disabled) and recreated exactly as they were afterwards. A typical test writes to a small
    /// fraction of the schema, so this is far cheaper than wiping every table. Because the tables survive,
    /// the provider's <c>EnsureCreated</c> builds the schema only on the first acquisition of a worker and
    /// is a cheap no-op on every clean thereafter.
    /// </summary>
    internal async Task CleanAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            cmdText: """
                     SET NOCOUNT ON;

                     DECLARE @nonEmptyTables TABLE (object_id INT PRIMARY KEY);
                     INSERT INTO @nonEmptyTables (object_id)
                     SELECT t.object_id
                     FROM sys.tables t
                     WHERE t.name <> N'__EFMigrationsHistory'
                       AND t.is_ms_shipped = 0
                       AND EXISTS (
                           SELECT 1
                           FROM sys.dm_db_partition_stats ps
                           WHERE ps.object_id = t.object_id
                             AND ps.index_id IN (0, 1)
                             AND ps.row_count > 0);

                     IF NOT EXISTS (SELECT 1 FROM @nonEmptyTables)
                         RETURN;

                     -- Capture the DDL to drop and to restore every foreign key that references a table
                     -- about to be truncated: the restore text has to be read from the catalogue before
                     -- the constraints are dropped.
                     DECLARE @dropForeignKeys NVARCHAR(MAX) = N'';
                     SELECT @dropForeignKeys += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
                         + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
                     FROM sys.foreign_keys fk
                     INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
                     INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                     WHERE fk.referenced_object_id IN (SELECT object_id FROM @nonEmptyTables);

                     DECLARE @restoreForeignKeys NVARCHAR(MAX) = N'';
                     SELECT @restoreForeignKeys += N'ALTER TABLE ' + QUOTENAME(ps.name) + N'.' + QUOTENAME(pt.name)
                         + N' ADD CONSTRAINT ' + QUOTENAME(fk.name) + N' FOREIGN KEY ('
                         + (SELECT STRING_AGG(QUOTENAME(pc.name), N',') WITHIN GROUP (ORDER BY fkc.constraint_column_id)
                            FROM sys.foreign_key_columns fkc
                            INNER JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
                            WHERE fkc.constraint_object_id = fk.object_id)
                         + N') REFERENCES ' + QUOTENAME(rs.name) + N'.' + QUOTENAME(rt.name) + N' ('
                         + (SELECT STRING_AGG(QUOTENAME(rc.name), N',') WITHIN GROUP (ORDER BY fkc.constraint_column_id)
                            FROM sys.foreign_key_columns fkc
                            INNER JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
                            WHERE fkc.constraint_object_id = fk.object_id)
                         + N')'
                         + CASE fk.delete_referential_action
                             WHEN 1 THEN N' ON DELETE CASCADE' WHEN 2 THEN N' ON DELETE SET NULL' WHEN 3 THEN N' ON DELETE SET DEFAULT' ELSE N'' END
                         + CASE fk.update_referential_action
                             WHEN 1 THEN N' ON UPDATE CASCADE' WHEN 2 THEN N' ON UPDATE SET NULL' WHEN 3 THEN N' ON UPDATE SET DEFAULT' ELSE N'' END
                         + N';'
                     FROM sys.foreign_keys fk
                     INNER JOIN sys.tables pt ON fk.parent_object_id = pt.object_id
                     INNER JOIN sys.schemas ps ON pt.schema_id = ps.schema_id
                     INNER JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
                     INNER JOIN sys.schemas rs ON rt.schema_id = rs.schema_id
                     WHERE fk.referenced_object_id IN (SELECT object_id FROM @nonEmptyTables);

                     DECLARE @truncateTables NVARCHAR(MAX) = N'';
                     SELECT @truncateTables += N'TRUNCATE TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';'
                     FROM sys.tables t
                     INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                     WHERE t.object_id IN (SELECT object_id FROM @nonEmptyTables);

                     EXEC sp_executesql @dropForeignKeys;
                     EXEC sp_executesql @truncateTables;
                     EXEC sp_executesql @restoreForeignKeys;
                     """, connection);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal ValueTask DisposeContainerAsync()
    {
        return Container.DisposeAsync();
    }

    /// <summary>
    /// A no-op wait strategy that defers to the container merely being started. Login readiness is then
    /// confirmed host-side by <see cref="WaitUntilReadyAsync"/>, avoiding the module's default in-container
    /// sqlcmd probe (absent from the Azure SQL Edge image).
    /// </summary>
    private sealed class ContainerRunningWaitStrategy : IWaitUntil
    {
        public Task<bool> UntilAsync(IContainer container)
        {
            return Task.FromResult(true);
        }
    }
}
