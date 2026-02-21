using Eksen.UnitOfWork;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Eksen.EntityFrameworkCore;

public class DbContextTrackerSaveChangesInterceptor(
    IUnitOfWorkManager unitOfWorkManager,
    IDbContextTracker dbContextTracker
) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        var scope = unitOfWorkManager.Current;

        if (eventData.Context != null && scope != null)
        {
            dbContextTracker.TrackDbContext(scope, eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = new())
    {
        var scope = unitOfWorkManager.Current;

        if (eventData.Context != null && scope != null)
        {
            dbContextTracker.TrackDbContext(scope, eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}