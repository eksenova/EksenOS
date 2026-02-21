using Eksen.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Eksen.EntityFrameworkCore;

public interface IDbContextTracker
{
    IReadOnlyCollection<DbContext> GetScopeDbContexts(IUnitOfWorkScope scope);

    void TrackDbContext(IUnitOfWorkScope scope, DbContext dbContext);

    void ClearScope(IUnitOfWorkScope scope);
}