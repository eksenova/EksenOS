using Eksen.UnitOfWork;
using IsolationLevel = System.Data.IsolationLevel;

namespace Eksen.EntityFrameworkCore;

public class EfCoreUnitOfWorkProvider(
    IDbContextTracker dbContextTracker
) : IUnitOfWorkProvider
{
    public IUnitOfWorkProviderScope BeginScope(
        IUnitOfWorkScope parent,
        bool isTransctional,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default)
    {
        return new EfCoreUnitOfWorkScope(this, parent, isTransctional, dbContextTracker, isolationLevel);
    }
}