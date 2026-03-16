using Npgsql;

namespace Eksen.DistributedLocks.PostgreSql;

internal sealed class PostgreSqlDistributedLockHandle(
    string name,
    long key,
    NpgsqlConnection connection) : IDistributedLockHandle
{
    private bool _isDisposed;

    public string Name { get; } = name;

    public bool IsAcquired => !_isDisposed;

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT pg_advisory_unlock(@key)", connection);
            command.Parameters.AddWithValue("key", key);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}
