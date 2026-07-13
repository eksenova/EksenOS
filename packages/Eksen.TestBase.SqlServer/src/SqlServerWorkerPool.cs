using System.Threading.Channels;
using Xunit;

namespace Eksen.TestBase.SqlServer;

/// <summary>
/// An assembly-shared, bounded pool of SQL Server containers. A single instance is created for the
/// whole test assembly (registered via <c>[assembly: AssemblyFixture(typeof(SqlServerWorkerPool))]</c>),
/// so that when collections and tests run in parallel each test acquires an idle worker and releases it
/// on completion. Idle workers are picked up by the next waiting test, and new workers are created lazily
/// up to <see cref="SqlServerTestEnvironment.MaxWorkers"/>. Container creation runs outside the
/// reservation lock so multiple workers start concurrently instead of queueing behind one another.
/// </summary>
public sealed class SqlServerWorkerPool : IAsyncLifetime
{
    private readonly Channel<SqlServerWorker> _available;
    private readonly Lock _reservationLock = new();
    private readonly List<SqlServerWorker> _workers = [];
    private readonly List<Task> _pendingReleases = [];
    private int _created;

    public SqlServerWorkerPool()
    {
        MaxWorkers = SqlServerTestEnvironment.MaxWorkers;
        _available = Channel.CreateBounded<SqlServerWorker>(MaxWorkers);
    }

    /// <summary>
    /// The maximum number of concurrent SQL Server containers this pool will run.
    /// </summary>
    public int MaxWorkers { get; }

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        Task[] pendingReleases;
        lock (_reservationLock)
        {
            pendingReleases = _pendingReleases.ToArray();
        }

        try
        {
            await Task.WhenAll(pendingReleases);
        }
        catch
        {
            // A failed release sanitizer already marked its worker dirty; disposal proceeds regardless.
        }

        foreach (var worker in _workers)
        {
            await worker.DisposeContainerAsync();
        }
    }

    /// <summary>
    /// Acquires an idle worker, creating a new container if the pool has not reached its bound,
    /// otherwise waiting for a worker to be released. The returned worker's database is clean:
    /// either its release-time sanitizer already ran, or the data is wiped here on acquisition.
    /// </summary>
    internal async Task<SqlServerWorker> AcquireAsync(CancellationToken cancellationToken = default)
    {
        if (_available.Reader.TryRead(out var worker))
        {
            await CleanIfRequiredAsync(worker, cancellationToken);
            return worker;
        }

        var reserved = false;
        lock (_reservationLock)
        {
            if (_created < MaxWorkers)
            {
                _created++;
                reserved = true;
            }
        }

        if (reserved)
        {
            try
            {
                worker = await SqlServerWorker.CreateAsync(cancellationToken);
            }
            catch
            {
                lock (_reservationLock)
                {
                    _created--;
                }

                throw;
            }

            lock (_reservationLock)
            {
                _workers.Add(worker);
            }

            return worker;
        }

        worker = await _available.Reader.ReadAsync(cancellationToken);
        await CleanIfRequiredAsync(worker, cancellationToken);
        return worker;
    }

    /// <summary>
    /// Acquires an idle worker and hands it back as a disposable <see cref="SqlServerConnectionLease"/>.
    /// This is the public seam for outside consumers (for example a <c>WebApplicationFactory</c> host)
    /// to share this assembly's bounded worker pool: lease a connection in set-up, point the host's
    /// <c>DbContext</c> at <see cref="SqlServerConnectionLease.ConnectionString"/>, and dispose the lease
    /// in tear-down to return the worker to the pool.
    /// </summary>
    public async Task<SqlServerConnectionLease> LeaseConnectionAsync(CancellationToken cancellationToken = default)
    {
        var worker = await AcquireAsync(cancellationToken);
        return new SqlServerConnectionLease(this, worker);
    }

    /// <summary>
    /// Returns a worker to the pool so an idle test can pick it up. The worker's data is wiped by the
    /// next acquisition.
    /// </summary>
    internal ValueTask ReleaseAsync(SqlServerWorker worker)
    {
        worker.RequiresCleaning = true;
        return _available.Writer.WriteAsync(worker);
    }

    /// <summary>
    /// Returns a worker to the pool after sanitizing it in the background, off the releasing test's
    /// critical path: <paramref name="quiesceAsync"/> (when given) settles any in-flight consumer
    /// activity first, the worker's data is wiped, and <paramref name="prepareAsync"/> then puts the
    /// database back into the consumer's ready-to-use baseline. A successfully sanitized worker
    /// re-enters the pool ready for immediate use; when any step throws, the worker re-enters dirty
    /// and the next acquisition falls back to the built-in data wipe.
    /// </summary>
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
            _pendingReleases.RemoveAll(t => t.IsCompleted);
            _pendingReleases.Add(release);
        }
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
