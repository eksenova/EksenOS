using Microsoft.EntityFrameworkCore.Storage;
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
            foreach (var providerScope in scope.ProviderScopes.OfType<EfCoreUnitOfWorkScope>())
            {
                providerScope.EnsureTransaction(eventData.Context);
            }
            command.Transaction = eventData.Context.Database.CurrentTransaction?.GetDbTransaction();
        }

        return base.ReaderExecuting(command, eventData, result);
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = new())
    {
        var scope = unitOfWorkManager.Current;

        if (eventData.Context != null && scope != null)
        {
            dbContextTracker.TrackDbContext(scope, eventData.Context);
            foreach (var providerScope in scope.ProviderScopes.OfType<EfCoreUnitOfWorkScope>())
            {
                await providerScope.EnsureTransactionAsync(eventData.Context, cancellationToken);
            }
            command.Transaction = eventData.Context.Database.CurrentTransaction?.GetDbTransaction();
        }

        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context && unitOfWorkManager.Current is { } scope)
        {
            dbContextTracker.TrackDbContext(scope, context);
            foreach (var providerScope in scope.ProviderScopes.OfType<EfCoreUnitOfWorkScope>())
            {
                providerScope.EnsureTransaction(context);
            }
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
        }

        return result;
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && unitOfWorkManager.Current is { } scope)
        {
            dbContextTracker.TrackDbContext(scope, context);
            foreach (var providerScope in scope.ProviderScopes.OfType<EfCoreUnitOfWorkScope>())
            {
                await providerScope.EnsureTransactionAsync(context, cancellationToken);
            }
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
        }

        return result;
    }
}