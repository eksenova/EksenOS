using Microsoft.Data.SqlClient;

namespace Eksen.TestBase.SqlServer;

internal sealed class SqlServerWorker
{
    internal SqlServerWorker(SqlServerInstance instance, string connectionString)
    {
        Instance = instance;
        ConnectionString = connectionString;
    }

    internal SqlServerInstance Instance { get; }

    internal string ConnectionString { get; }

    internal bool RequiresCleaning { get; set; }

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
}
