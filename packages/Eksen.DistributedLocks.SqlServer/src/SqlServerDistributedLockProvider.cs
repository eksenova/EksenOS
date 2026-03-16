using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Eksen.DistributedLocks.SqlServer;

internal sealed class SqlServerDistributedLockProvider(
    IOptions<SqlServerDistributedLockOptions> sqlOptions,
    IOptions<EksenDistributedLockOptions> lockOptions) : IDistributedLockProvider
{
    public async Task<IDistributedLockHandle> AcquireAsync(
        string? name = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        name ??= Guid.NewGuid().ToString("N");
        timeout ??= lockOptions.Value.DefaultTimeout;

        var connection = new SqlConnection(sqlOptions.Value.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var timeoutMs = timeout.HasValue
            ? (int)timeout.Value.TotalMilliseconds
            : -1; // -1 = wait indefinitely

        var result = await ExecuteGetAppLockAsync(connection, name, timeoutMs, cancellationToken);

        if (result < 0)
        {
            await connection.DisposeAsync();
            throw new DistributedLockException(
                $"Failed to acquire distributed lock '{name}'. sp_getapplock returned {result}.");
        }

        return new SqlServerDistributedLockHandle(name, connection);
    }

    public async Task<IDistributedLockHandle> TryAcquireAsync(
        string? name = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        name ??= Guid.NewGuid().ToString("N");
        timeout ??= lockOptions.Value.DefaultTimeout;

        var connection = new SqlConnection(sqlOptions.Value.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var timeoutMs = timeout.HasValue
            ? (int)timeout.Value.TotalMilliseconds
            : 0; // 0 = do not wait

        var result = await ExecuteGetAppLockAsync(connection, name, timeoutMs, cancellationToken);

        if (result < 0)
        {
            await connection.DisposeAsync();
            return new NotAcquiredDistributedLockHandle(name);
        }

        return new SqlServerDistributedLockHandle(name, connection);
    }

    /// <summary>
    /// Executes sp_getapplock and returns the result code.
    /// 0 = lock granted synchronously, 1 = lock granted after waiting.
    /// Negative values indicate failure: -1 = timed out, -2 = cancelled, -3 = deadlock, -999 = other.
    /// </summary>
    private static async Task<int> ExecuteGetAppLockAsync(
        SqlConnection connection,
        string name,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "sp_getapplock";
        command.CommandType = System.Data.CommandType.StoredProcedure;
        command.Parameters.AddWithValue("@Resource", name);
        command.Parameters.AddWithValue("@LockMode", "Exclusive");
        command.Parameters.AddWithValue("@LockOwner", "Session");
        command.Parameters.AddWithValue("@LockTimeout", timeoutMs);

        var returnValue = new SqlParameter
        {
            Direction = System.Data.ParameterDirection.ReturnValue,
            SqlDbType = System.Data.SqlDbType.Int
        };
        command.Parameters.Add(returnValue);

        await command.ExecuteNonQueryAsync(cancellationToken);

        return (int)returnValue.Value!;
    }
}
