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
    /// Wipes all data so the next test starts from empty tables while leaving the schema in place.
    /// Every foreign key is dropped (SQL Server refuses to <c>TRUNCATE</c> a table referenced by a
    /// foreign key even when the constraint is disabled), every user table except the EF migrations
    /// history is truncated, and the foreign keys are then recreated exactly as they were. Because
    /// the tables survive, the provider's <c>EnsureCreated</c> builds the schema only on the first
    /// acquisition of a worker and is a cheap no-op on every clean thereafter.
    /// </summary>
    internal async Task CleanAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(
            cmdText: """
                     SET NOCOUNT ON;

                     -- Capture the DDL to drop and to restore every foreign key up front: the restore
                     -- text has to be read from the catalogue before the constraints are dropped.
                     DECLARE @dropForeignKeys NVARCHAR(MAX) = N'';
                     SELECT @dropForeignKeys += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
                         + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
                     FROM sys.foreign_keys fk
                     INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
                     INNER JOIN sys.schemas s ON t.schema_id = s.schema_id;

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
                     INNER JOIN sys.schemas rs ON rt.schema_id = rs.schema_id;

                     -- Truncate every user table except the EF migrations history so the recorded
                     -- schema version survives the clean.
                     DECLARE @truncateTables NVARCHAR(MAX) = N'';
                     SELECT @truncateTables += N'TRUNCATE TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';'
                     FROM sys.tables t
                     INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                     WHERE t.name <> N'__EFMigrationsHistory' AND t.is_ms_shipped = 0;

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
}
