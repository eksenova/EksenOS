using System.IO.Hashing;
using System.Text;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Eksen.DistributedLocks.PostgreSql;

internal sealed class PostgreSqlDistributedLockProvider(
    IOptions<PostgreSqlDistributedLockOptions> pgOptions,
    IOptions<EksenDistributedLockOptions> lockOptions) : IDistributedLockProvider
{
    public async Task<IDistributedLockHandle> AcquireAsync(
        string? name = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        name ??= Guid.NewGuid().ToString("N");
        var key = ComputeAdvisoryLockKey(name);
        timeout ??= lockOptions.Value.DefaultTimeout;

        var connection = new NpgsqlConnection(pgOptions.Value.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        if (timeout.HasValue)
        {
            var deadline = DateTimeOffset.UtcNow + timeout.Value;

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await TryAcquireLockAsync(connection, key, cancellationToken))
                {
                    return new PostgreSqlDistributedLockHandle(name, key, connection);
                }

                var remaining = deadline - DateTimeOffset.UtcNow;
                var delay = TimeSpan.FromMilliseconds(Math.Min(50, remaining.TotalMilliseconds));

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }

            await connection.DisposeAsync();
            throw new DistributedLockException(
                $"Failed to acquire distributed lock '{name}' within the specified timeout of {timeout.Value}.");
        }

        await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", connection);
        command.Parameters.AddWithValue("key", key);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new PostgreSqlDistributedLockHandle(name, key, connection);
    }

    public async Task<IDistributedLockHandle> TryAcquireAsync(
        string? name = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        name ??= Guid.NewGuid().ToString("N");
        var key = ComputeAdvisoryLockKey(name);
        timeout ??= lockOptions.Value.DefaultTimeout;

        var connection = new NpgsqlConnection(pgOptions.Value.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        if (timeout.HasValue)
        {
            var deadline = DateTimeOffset.UtcNow + timeout.Value;

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await TryAcquireLockAsync(connection, key, cancellationToken))
                {
                    return new PostgreSqlDistributedLockHandle(name, key, connection);
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
                return new PostgreSqlDistributedLockHandle(name, key, connection);
            }
        }

        await connection.DisposeAsync();
        return new NotAcquiredDistributedLockHandle(name);
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
