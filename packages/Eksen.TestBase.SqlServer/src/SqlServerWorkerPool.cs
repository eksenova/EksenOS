using System.Threading.Channels;
using Xunit;

namespace Eksen.TestBase.SqlServer;

public sealed class SqlServerWorkerPool : IAsyncLifetime
{
    private readonly Channel<SqlServerWorker> _available;
    private readonly Lock _reservationLock = new();
    private readonly List<SqlServerInstance> _instances = [];
    private readonly List<Task> _pendingReleases = [];
    private int _created;

    public SqlServerWorkerPool()
    {
        MaxWorkers = SqlServerTestEnvironment.MaxWorkers;
        DatabasesPerInstance = SqlServerTestEnvironment.DatabasesPerInstance;
        _available = Channel.CreateBounded<SqlServerWorker>(MaxWorkers);
    }

    public int MaxWorkers { get; }

    public int DatabasesPerInstance { get; }

    public int InstanceCount
    {
        get
        {
            lock (_reservationLock)
            {
                return _instances.Count;
            }
        }
    }

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await WaitForPendingReleasesIgnoringFailuresAsync();

        foreach (var instance in _instances)
        {
            await instance.DisposeContainerAsync();
        }
    }

    internal async Task<SqlServerWorker> AcquireAsync(CancellationToken cancellationToken = default)
    {
        if (_available.Reader.TryRead(out var worker))
        {
            await CleanIfRequiredAsync(worker, cancellationToken);
            return worker;
        }

        var reservedDatabaseCount = ReserveInstanceDatabases();

        if (reservedDatabaseCount > 0)
        {
            return await CreateInstanceWorkersAsync(reservedDatabaseCount, cancellationToken);
        }

        worker = await _available.Reader.ReadAsync(cancellationToken);
        await CleanIfRequiredAsync(worker, cancellationToken);
        return worker;
    }

    public async Task<SqlServerConnectionLease> LeaseConnectionAsync(CancellationToken cancellationToken = default)
    {
        var worker = await AcquireAsync(cancellationToken);
        return new SqlServerConnectionLease(this, worker);
    }

    internal ValueTask ReleaseAsync(SqlServerWorker worker)
    {
        worker.RequiresCleaning = true;
        return _available.Writer.WriteAsync(worker);
    }

    internal void ReleaseSanitized(
        SqlServerWorker worker,
        Func<Task>? quiesceAsync,
        Func<Task> prepareAsync)
    {
        var release = Task.Run(async () =>
        {
            try
            {
                if (quiesceAsync is not null)
                {
                    await quiesceAsync();
                }

                await worker.CleanAsync();
                await prepareAsync();
                worker.RequiresCleaning = false;
            }
            catch
            {
                worker.RequiresCleaning = true;
                throw;
            }
            finally
            {
                await _available.Writer.WriteAsync(worker);
            }
        });

        lock (_reservationLock)
        {
            _pendingReleases.RemoveAll(task => task.IsCompleted);
            _pendingReleases.Add(release);
        }
    }

    private async Task WaitForPendingReleasesIgnoringFailuresAsync()
    {
        Task[] pendingReleases;
        lock (_reservationLock)
        {
            pendingReleases = _pendingReleases.ToArray();
        }

        foreach (var pendingRelease in pendingReleases)
        {
            try
            {
                await pendingRelease;
            }
            catch (Exception)
            {
                continue;
            }
        }
    }

    private int ReserveInstanceDatabases()
    {
        lock (_reservationLock)
        {
            var remaining = MaxWorkers - _created;
            if (remaining <= 0)
            {
                return 0;
            }

            var reserved = Math.Min(DatabasesPerInstance, remaining);
            _created += reserved;
            return reserved;
        }
    }

    private async Task<SqlServerWorker> CreateInstanceWorkersAsync(
        int databaseCount,
        CancellationToken cancellationToken)
    {
        SqlServerInstance instance;

        try
        {
            instance = await SqlServerInstance.CreateAsync(databaseCount, cancellationToken);
        }
        catch
        {
            lock (_reservationLock)
            {
                _created -= databaseCount;
            }

            throw;
        }

        var workers = instance.DatabaseConnectionStrings
            .Select(connectionString => new SqlServerWorker(instance, connectionString))
            .ToArray();

        lock (_reservationLock)
        {
            _instances.Add(instance);
        }

        foreach (var spare in workers.Skip(count: 1))
        {
            await _available.Writer.WriteAsync(spare, cancellationToken);
        }

        return workers[0];
    }

    private static async Task CleanIfRequiredAsync(SqlServerWorker worker, CancellationToken cancellationToken)
    {
        if (worker.RequiresCleaning)
        {
            await worker.CleanAsync(cancellationToken);
            worker.RequiresCleaning = false;
        }
    }
}
