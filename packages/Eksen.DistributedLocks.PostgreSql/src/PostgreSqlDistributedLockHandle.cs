using System.Diagnostics;
using Npgsql;

namespace Eksen.DistributedLocks.PostgreSql;

internal sealed class PostgreSqlDistributedLockHandle(
    string name,
    long key,
    NpgsqlConnection connection,
    DistributedLocksTelemetry? telemetry = null,
    string? metricLockName = null) : IDistributedLockHandle
{
    private readonly long _acquiredAt = Stopwatch.GetTimestamp();
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
            telemetry?.RecordHold(metricLockName ?? Name, _acquiredAt);
            await connection.DisposeAsync();
        }
    }
}
