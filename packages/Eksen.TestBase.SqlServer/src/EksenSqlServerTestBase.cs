using Eksen.TestBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Eksen.TestBase.SqlServer;

/// <summary>
/// Base class for integration tests that run against a real SQL Server database, backed by the
/// assembly-shared <see cref="SqlServerWorkerPool"/>. Each test acquires an idle worker from the pool
/// for the duration of the test and releases it on disposal, allowing many tests across parallel
/// collections to share a small, bounded set of containers.
/// </summary>
/// <typeparam name="TDbContext">The EF Core context under test.</typeparam>
/// <remarks>
/// The consuming test assembly must register the pool once:
/// <code>[assembly: AssemblyFixture(typeof(SqlServerWorkerPool))]</code>
/// The pool is then injected into derived test classes through their constructor.
/// </remarks>
public abstract class EksenSqlServerTestBase<TDbContext>(SqlServerWorkerPool pool)
    : EksenDatabaseTestBase<TDbContext>
    where TDbContext : DbContext
{
    private SqlServerWorker? _worker;

    protected string ConnectionString =>
        _worker?.ConnectionString
        ?? throw new InvalidOperationException("No SQL Server worker has been acquired yet.");

    protected override async Task<string> GetConnectionStringAsync()
    {
        _worker = await pool.AcquireAsync();
        return _worker.ConnectionString;
    }

    protected override void ConfigureDbContext(
        IEksenEntityFrameworkCoreBuilder efCoreBuilder,
        string connectionString)
    {
        efCoreBuilder.UseSqlServerDbContext<TDbContext>(connectionString);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_worker is not null)
        {
            await pool.ReleaseAsync(_worker);
            _worker = null;
        }

        await base.DisposeAsync();
    }
}
