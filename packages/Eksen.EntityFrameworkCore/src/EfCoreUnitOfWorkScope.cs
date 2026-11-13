using System.Data;
using System.Diagnostics;
using Eksen.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Eksen.EntityFrameworkCore;

public class EfCoreUnitOfWorkScope(
    EfCoreUnitOfWorkProvider provider,
    IUnitOfWorkScope parentScope,
    bool isTransactional,
    IDbContextTracker dbContextTracker,
    IsolationLevel? isolationLevel) : IUnitOfWorkProviderScope
{
    private readonly Dictionary<DbContext, IDbContextTransaction> _transactions = new();
    private bool _isCommitted;
    private bool _isRolledBack;
    private bool _isDisposed;

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            if (!_isCommitted && !_isRolledBack)
            {
                if (isTransactional)
                {
                    await RollbackAsync();
                }
                else
                {
                    await CommitAsync();
                }
            }
        }
        finally
        {
            try
            {
                await DisposeTransactionsAsync();
            }
            finally
            {
                dbContextTracker.ClearScope(ParentScope);
                _isDisposed = true;
            }
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        EnsureActive();
        try
        {
            await SaveChangesInternalAsync(isTransactional, cancellationToken);
            foreach (var (context, transaction) in _transactions)
            {
                await transaction.CommitAsync(cancellationToken);
                AddTransactionEvent("tx.commit", context);
            }

            _isCommitted = true;
        }
        catch (Exception exception)
        {
            try
            {
                await RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackException)
            {
                exception.Data["RollbackException"] = rollbackException;
            }
            throw;
        }
        finally
        {
            await DisposeTransactionsAsync();
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_isCommitted)
        {
            throw new InvalidOperationException("Cannot rollback a committed unit of work.");
        }

        if (_isRolledBack)
        {
            return;
        }

        _isRolledBack = true;
        try
        {
            foreach (var (context, transaction) in _transactions)
            {
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    AddTransactionEvent("tx.rollback", context);
                }
            }
        }
        finally
        {
            foreach (var context in dbContextTracker.GetScopeDbContexts(ParentScope))
            {
                context.ChangeTracker.Clear();
            }

            await DisposeTransactionsAsync();
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnsureActive();
        return SaveChangesInternalAsync(isTransactional, cancellationToken);
    }

    internal void EnsureTransaction(DbContext context)
    {
        EnsureActive();
        if (isTransactional && !_transactions.ContainsKey(context))
        {
            var transaction = isolationLevel.HasValue
                ? context.Database.BeginTransaction(isolationLevel.Value)
                : context.Database.BeginTransaction();
            _transactions.Add(context, transaction);
            AddTransactionEvent("tx.begin", context);
        }
    }

    internal async Task EnsureTransactionAsync(DbContext context, CancellationToken cancellationToken)
    {
        EnsureActive();
        if (isTransactional && !_transactions.ContainsKey(context))
        {
            var transaction = isolationLevel.HasValue
                ? await context.Database.BeginTransactionAsync(isolationLevel.Value, cancellationToken)
                : await context.Database.BeginTransactionAsync(cancellationToken);
            _transactions.Add(context, transaction);
            AddTransactionEvent("tx.begin", context);
        }
    }

    protected virtual async Task SaveChangesInternalAsync(bool createTransactionIfNotExists, CancellationToken cancellationToken = default)
    {
        foreach (var context in dbContextTracker.GetScopeDbContexts(ParentScope))
        {
            if (createTransactionIfNotExists)
            {
                await EnsureTransactionAsync(context, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private void AddTransactionEvent(string name, DbContext context)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        var tags = new ActivityTagsCollection
        {
            { "eksen.db.context", context.GetType().Name }
        };
        if (isolationLevel.HasValue)
        {
            tags.Add("db.transaction.isolation_level", isolationLevel.Value.ToString());
        }

        activity.AddEvent(new ActivityEvent(name, tags: tags));
    }

    private void EnsureActive()
    {
        if (_isCommitted)
        {
            throw new InvalidOperationException("This unit of work scope has already been committed.");
        }

        if (_isRolledBack || _isDisposed)
        {
            throw new InvalidOperationException("This unit of work scope has already finished.");
        }
    }

    private async Task DisposeTransactionsAsync()
    {
        try
        {
            foreach (var transaction in _transactions.Values)
            {
                await transaction.DisposeAsync();
            }
        }
        finally
        {
            _transactions.Clear();
        }
    }

    public IUnitOfWorkProvider Provider { get; } = provider;

    public IUnitOfWorkScope ParentScope { get; } = parentScope;
}