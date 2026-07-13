namespace Eksen.TestBase.SqlServer;

/// <summary>
/// A leased SQL Server connection drawn from the assembly-shared <see cref="SqlServerWorkerPool"/>.
/// Exposes the freshly cleaned <see cref="ConnectionString"/> of an acquired worker and returns that
/// worker to the pool on <see cref="DisposeAsync"/>. This is the seam through which outside consumers
/// (for example a <c>WebApplicationFactory</c> host) share the same bounded worker pool as the
/// integration-test base classes, instead of standing up their own containers.
/// </summary>
public sealed class SqlServerConnectionLease : IAsyncDisposable
{
    private readonly SqlServerWorkerPool _pool;
    private SqlServerWorker? _worker;

    internal SqlServerConnectionLease(SqlServerWorkerPool pool, SqlServerWorker worker)
    {
        _pool = pool;
        _worker = worker;
    }

    /// <summary>
    /// The connection string of the leased worker's freshly cleaned database.
    /// </summary>
    public string ConnectionString =>
        _worker?.ConnectionString
        ?? throw new ObjectDisposedException(nameof(SqlServerConnectionLease));

    /// <summary>
    /// Returns the leased worker to the pool so the next waiting test or consumer can acquire it.
    /// The worker's data is wiped by the next acquisition.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_worker is not null)
        {
            await _pool.ReleaseAsync(_worker);
            _worker = null;
        }
    }

    /// <summary>
    /// Returns the leased worker to the pool after sanitizing it in the background, off the releasing
    /// test's critical path: <paramref name="quiesceAsync"/> (when given) settles any in-flight consumer
    /// activity, the pool wipes the worker's data, and <paramref name="prepareAsync"/> then puts the
    /// database back into the consumer's ready-to-use baseline (reseeded, caches coherent). When any
    /// step throws, the worker re-enters the pool dirty and the next acquisition falls back to the
    /// built-in data wipe.
    /// </summary>
    public void DisposeSanitized(Func<Task>? quiesceAsync, Func<Task> prepareAsync)
    {
        if (_worker is null)
        {
            return;
        }

        var worker = _worker;
        _worker = null;
        _pool.ReleaseSanitized(worker, quiesceAsync, prepareAsync);
    }
}
