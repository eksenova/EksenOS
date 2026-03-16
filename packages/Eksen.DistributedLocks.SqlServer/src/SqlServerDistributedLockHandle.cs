using Microsoft.Data.SqlClient;

namespace Eksen.DistributedLocks.SqlServer;

internal sealed class SqlServerDistributedLockHandle(
    string name,
    SqlConnection connection) : IDistributedLockHandle
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
            await using var command = connection.CreateCommand();
            command.CommandText = "sp_releaseapplock";
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Resource", Name);
            command.Parameters.AddWithValue("@LockOwner", "Session");
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}
