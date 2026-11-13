using Eksen.UnitOfWork;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Eksen.EntityFrameworkCore;

public class DbContextTrackerSaveChangesInterceptor(
    IUnitOfWorkManager unitOfWorkManager,
    IDbContextTracker dbContextTracker
) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context != null)
        {
            TrackDbContext(eventData.Context);
            foreach (var providerScope in GetProviderScopes())
            {
                providerScope.EnsureTransaction(eventData.Context);
            }
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = new())
    {
        if (eventData.Context != null)
        {
            TrackDbContext(eventData.Context);
            foreach (var providerScope in GetProviderScopes())
            {
                await providerScope.EnsureTransactionAsync(eventData.Context, cancellationToken);
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    internal void TrackDbContext(DbContext context)
    {
        if (unitOfWorkManager.Current is { } scope)
        {
            dbContextTracker.TrackDbContext(scope, context);
        }
    }

    private IEnumerable<EfCoreUnitOfWorkScope> GetProviderScopes()
    {
        return unitOfWorkManager.Current?.ProviderScopes.OfType<EfCoreUnitOfWorkScope>() ?? [];
    }
}
