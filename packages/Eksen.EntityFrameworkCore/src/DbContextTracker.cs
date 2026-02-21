using Eksen.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Eksen.EntityFrameworkCore;

public sealed class DbContextTracker : IDbContextTracker
{
    private readonly Dictionary<IUnitOfWorkScope, HashSet<DbContext>> _dbContexts = [];

    public IReadOnlyCollection<DbContext> GetScopeDbContexts(IUnitOfWorkScope scope)
    {
        return _dbContexts[scope].AsReadOnly();
    }

    public void TrackDbContext(IUnitOfWorkScope scope, DbContext dbContext)
    {
        if (!_dbContexts.ContainsKey(scope))
        {
            _dbContexts[scope] = [];
        }

        _dbContexts[scope].Add(dbContext);
    }

    public void ClearScope(IUnitOfWorkScope scope)
    {
        _dbContexts[scope].Clear();
        _dbContexts.Remove(scope);
    }
}