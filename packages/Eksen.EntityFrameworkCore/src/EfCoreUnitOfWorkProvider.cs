using Eksen.UnitOfWork;
using IsolationLevel = System.Data.IsolationLevel;

namespace Eksen.EntityFrameworkCore;

public class EfCoreUnitOfWorkProvider(
    IDbContextTracker dbContextTracker
) : IUnitOfWorkProvider
{
    public Task<IUnitOfWorkProviderScope> BeginScopeAsync(
        IUnitOfWorkScope parent,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IUnitOfWorkProviderScope>(
            new EfCoreUnitOfWorkScope(this, parent, dbContextTracker, isolationLevel));
    }

    public void PopScope(IUnitOfWorkProviderScope scope)
    {
        dbContextTracker.ClearScope(scope.ParentScope);
    }
}