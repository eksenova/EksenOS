using System.Threading.Channels;
using Xunit;

namespace Eksen.TestBase.SqlServer;

/// <summary>
/// An assembly-shared, bounded pool of SQL Server containers. A single instance is created for the
/// whole test assembly (registered via <c>[assembly: AssemblyFixture(typeof(SqlServerWorkerPool))]</c>),
/// so that when collections and tests run in parallel each test acquires an idle worker and releases it
/// on completion. Idle workers are picked up by the next waiting test, and new workers are created lazily
/// up to <see cref="SqlServerTestEnvironment.MaxWorkers"/>.
/// </summary>
public sealed class SqlServerWorkerPool : IAsyncLifetime
{
    private readonly Channel<SqlServerWorker> _available;
    private readonly SemaphoreSlim _creationGate = new(initialCount: 1, maxCount: 1);
    private readonly List<SqlServerWorker> _workers = [];
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
        foreach (var worker in _workers)
        {
            await worker.DisposeContainerAsync();
        }

        _creationGate.Dispose();
    }

    /// <summary>
    /// Acquires an idle worker, creating a new container if the pool has not reached its bound,
    /// otherwise waiting for a worker to be released. The returned worker is freshly cleaned.
    /// </summary>
    internal async Task<SqlServerWorker> AcquireAsync(CancellationToken cancellationToken = default)
    {
        if (_available.Reader.TryRead(out var worker))
        {
            await worker.CleanAsync(cancellationToken);
            return worker;
        }

        await _creationGate.WaitAsync(cancellationToken);
        try
        {
            if (_available.Reader.TryRead(out worker))
            {
                await worker.CleanAsync(cancellationToken);
                return worker;
            }

            if (_created < MaxWorkers)
            {
                worker = await SqlServerWorker.CreateAsync(cancellationToken);
                _created++;
                _workers.Add(worker);
                return worker;
            }
        }
        finally
        {
            _creationGate.Release();
        }

        worker = await _available.Reader.ReadAsync(cancellationToken);
        await worker.CleanAsync(cancellationToken);
        return worker;
    }

    /// <summary>
    /// Returns a worker to the pool so an idle test can pick it up.
    /// </summary>
    internal ValueTask ReleaseAsync(SqlServerWorker worker)
    {
        return _available.Writer.WriteAsync(worker);
    }
}
