using Eksen.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Eksen.EntityFrameworkCore;

public sealed class DbContextTracker : IDbContextTracker
{
    private readonly Dictionary<IUnitOfWorkScope, HashSet<DbContext>> _scopeDbContexts = [];

    public IReadOnlyCollection<DbContext> GetScopeDbContexts(IUnitOfWorkScope scope)
    {
        return _scopeDbContexts.TryGetValue(scope, out var context)
            ? context.AsReadOnly()
            : [];
    }

    public void TrackDbContext(IUnitOfWorkScope scope, DbContext dbContext)
    {
        if (!_scopeDbContexts.ContainsKey(scope))
        {
            _scopeDbContexts[scope] = [];
        }

        _scopeDbContexts[scope].Add(dbContext);
    }

    public void ClearScope(IUnitOfWorkScope scope)
    {
        if (!_scopeDbContexts.TryGetValue(scope, out var contexts))
        {
            return;
        }

        contexts.Clear();
        _scopeDbContexts.Remove(scope);
    }
}