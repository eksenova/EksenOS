using System.Diagnostics;
using System.IO.Hashing;
using System.Text;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Eksen.DistributedLocks.PostgreSql;

internal sealed class PostgreSqlDistributedLockProvider(
    IOptions<PostgreSqlDistributedLockOptions> pgOptions,
    IOptions<EksenDistributedLockOptions> lockOptions,
    DistributedLocksTelemetry? telemetry = null) : IDistributedLockProvider
{
    private readonly DistributedLocksTelemetry _telemetry = telemetry ?? DistributedLocksTelemetry.Shared;

    public async Task<IDistributedLockHandle> AcquireAsync(
        string? name = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var metricLockName = MetricLockName(name);
        name ??= Guid.NewGuid().ToString("N");
        var key = ComputeAdvisoryLockKey(name);
        timeout ??= lockOptions.Value.DefaultTimeout;
        var waitStartedAt = Stopwatch.GetTimestamp();

        var connection = new NpgsqlConnection(pgOptions.Value.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);

            if (timeout.HasValue)
            {
                var deadline = DateTimeOffset.UtcNow + timeout.Value;

                while (DateTimeOffset.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (await TryAcquireLockAsync(connection, key, cancellationToken))
                    {
                        return Acquired(name, key, connection, metricLockName, waitStartedAt);
                    }

                    var remaining = deadline - DateTimeOffset.UtcNow;
                    var delay = TimeSpan.FromMilliseconds(Math.Min(50, remaining.TotalMilliseconds));

                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                }

                await connection.DisposeAsync();
                _telemetry.RecordAcquisition(metricLockName, "timeout", waitStartedAt);
                throw new DistributedLockException(
                    $"Failed to acquire distributed lock '{name}' within the specified timeout of {timeout.Value}.");
            }

            await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", connection);
            command.Parameters.AddWithValue("key", key);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return Acquired(name, key, connection, metricLockName, waitStartedAt);
        }
        catch (Exception exception) when (exception is not DistributedLockException)
        {
            await connection.DisposeAsync();
            _telemetry.RecordAcquisition(metricLockName, FailureResult(exception), waitStartedAt);
            throw;
        }
    }

    public async Task<IDistributedLockHandle> TryAcquireAsync(
        string? name = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var metricLockName = MetricLockName(name);
        name ??= Guid.NewGuid().ToString("N");
        var key = ComputeAdvisoryLockKey(name);
        timeout ??= lockOptions.Value.DefaultTimeout;
        var waitStartedAt = Stopwatch.GetTimestamp();

        var connection = new NpgsqlConnection(pgOptions.Value.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);

            if (timeout.HasValue)
            {
                var deadline = DateTimeOffset.UtcNow + timeout.Value;

                while (DateTimeOffset.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (await TryAcquireLockAsync(connection, key, cancellationToken))
                    {
                        return Acquired(name, key, connection, metricLockName, waitStartedAt);
                    }

                    var remaining = deadline - DateTimeOffset.UtcNow;
                    var delay = TimeSpan.FromMilliseconds(Math.Min(50, remaining.TotalMilliseconds));

                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                }
            }
            else
            {
                if (await TryAcquireLockAsync(connection, key, cancellationToken))
                {
                    return Acquired(name, key, connection, metricLockName, waitStartedAt);
                }
            }
        }
        catch (Exception exception)
        {
            await connection.DisposeAsync();
            _telemetry.RecordAcquisition(metricLockName, FailureResult(exception), waitStartedAt);
            throw;
        }

        await connection.DisposeAsync();
        _telemetry.RecordAcquisition(metricLockName, timeout.HasValue ? "timeout" : "not_acquired", waitStartedAt);
        return new NotAcquiredDistributedLockHandle(name);
    }

    private PostgreSqlDistributedLockHandle Acquired(
        string name,
        long key,
        NpgsqlConnection connection,
        string metricLockName,
        long waitStartedAt)
    {
        _telemetry.RecordAcquisition(metricLockName, "acquired", waitStartedAt);
        return new PostgreSqlDistributedLockHandle(name, key, connection, _telemetry, metricLockName);
    }

    private string MetricLockName(string? name)
    {
        return name is null
            ? DistributedLocksTelemetry.AnonymousLockName
            : pgOptions.Value.ResolveMetricLockName(name);
    }

    private static string FailureResult(Exception exception)
    {
        return exception is OperationCanceledException ? "canceled" : "error";
    }

    private static async Task<bool> TryAcquireLockAsync(
        NpgsqlConnection connection,
        long key,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection);
        command.Parameters.AddWithValue("key", key);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    private static long ComputeAdvisoryLockKey(string name)
    {
        var hash = XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(name));
        return unchecked((long)hash);
    }
}
