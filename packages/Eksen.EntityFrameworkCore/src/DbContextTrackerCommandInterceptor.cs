using System.Data.Common;
using Eksen.UnitOfWork;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Eksen.EntityFrameworkCore;

public class DbContextTrackerCommandInterceptor(
    IUnitOfWorkManager unitOfWorkManager,
    IDbContextTracker dbContextTracker
) : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        var scope = unitOfWorkManager.Current;

        if (eventData.Context != null && scope != null)
        {
            dbContextTracker.TrackDbContext(scope, eventData.Context);
        }

        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = new())
    {
        var scope = unitOfWorkManager.Current;

        if (eventData.Context != null && scope != null)
        {
            dbContextTracker.TrackDbContext(scope, eventData.Context);
        }

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}