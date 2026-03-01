using System.Data;
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
    private bool _isCommited;

    public async ValueTask DisposeAsync()
    {
        if (!_isCommited)
        {
            await CommitAsync();
        }

        dbContextTracker.ClearScope(ParentScope);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_isCommited)
        {
            throw new InvalidOperationException(message: "This unit of work scope has already been committed.");
        }

        if (!isTransactional || !_transactions.Any())
        {
            await SaveChangesInternalAsync(createTransactionIfNotExists: false, cancellationToken);
        }
        else
        {
            await SaveChangesInternalAsync(createTransactionIfNotExists: false, cancellationToken);

            var commitTasks = _transactions.Values
                .Select(t => t.CommitAsync(cancellationToken));

            foreach (var task in commitTasks)
            {
                await task;
            }

            _transactions.Clear();
        }

        _isCommited = true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_isCommited)
        {
            throw new InvalidOperationException(message: "Cannot rollback a committed unit of work.");
        }

        if (!isTransactional || !_transactions.Any())
        {
            return;
        }

        foreach (var (dbContext, transaction) in _transactions.ToList())
        {
            await transaction.RollbackAsync(cancellationToken);
            _transactions.Remove(dbContext);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesInternalAsync(isTransactional, cancellationToken);
    }

    protected virtual async Task SaveChangesInternalAsync(bool createTransactionIfNotExists, CancellationToken cancellationToken = default)
    {
        var dbContexts = dbContextTracker.GetScopeDbContexts(ParentScope);

        foreach (var dbContext in dbContexts)
        {
            if (createTransactionIfNotExists && !_transactions.ContainsKey(dbContext))
            {
                var transaction = await dbContext.Database.BeginTransactionAsync(
                    isolationLevel ?? IsolationLevel.ReadCommitted,
                    cancellationToken
                );

                _transactions.Add(dbContext, transaction);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public IUnitOfWorkProvider Provider { get; } = provider;

    public IUnitOfWorkScope ParentScope { get; } = parentScope;
}