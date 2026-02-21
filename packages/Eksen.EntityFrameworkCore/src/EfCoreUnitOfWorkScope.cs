using System.Data;
using Eksen.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Eksen.EntityFrameworkCore;

public class EfCoreUnitOfWorkScope(
    EfCoreUnitOfWorkProvider provider,
    IUnitOfWorkScope parentScope,
    IDbContextTracker dbContextTracker,
    IsolationLevel? isolationLevel
) : IUnitOfWorkProviderScope
{
    private readonly Dictionary<DbContext, IDbContextTransaction> _transactions = new();
    private bool _isCommited;

    public async ValueTask DisposeAsync()
    {
        foreach (var transaction in _transactions)
        {
            await transaction.Value.DisposeAsync();
        }

        _transactions.Clear();
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_isCommited)
        {
            throw new InvalidOperationException(message: "This unit of work scope has already been committed.");
        }

        if (!_transactions.Any())
        {
            await SaveChangesInternalAsync(createTransactionIfNotExists: false, cancellationToken);
        }
        else
        {
            var commitTasks = _transactions.Values
                .Select(t => t.CommitAsync(cancellationToken));

            await Task.WhenAll(commitTasks);

            _transactions.Clear();
        }

        _isCommited = true;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_isCommited)
        {
            throw new InvalidOperationException(message: "Cannot rollback a committed unit of work.");
        }

        if (!_transactions.Any())
        {
            return Task.CompletedTask;
        }

        var rollbackTasks = _transactions.Values
            .Select(t => t.RollbackAsync(cancellationToken));

        _transactions.Clear();

        return Task.WhenAll(rollbackTasks);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesInternalAsync(createTransactionIfNotExists: true, cancellationToken);
    }

    protected async Task SaveChangesInternalAsync(bool createTransactionIfNotExists, CancellationToken cancellationToken = default)
    {
        var dbContexts = dbContextTracker.GetScopeDbContexts(ParentScope);
        var saveChangesTasks = dbContexts.Select(async dbContext =>
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
        });

        await Task.WhenAll(saveChangesTasks);
    }

    public IUnitOfWorkProvider Provider { get; } = provider;

    public IUnitOfWorkScope ParentScope { get; } = parentScope;
}