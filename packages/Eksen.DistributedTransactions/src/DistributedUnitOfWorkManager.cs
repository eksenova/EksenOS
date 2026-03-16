using System.Data;
using Eksen.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;

namespace Eksen.DistributedTransactions;

internal sealed class DistributedUnitOfWorkManager(
    IServiceProvider serviceProvider,
    IDistributedTransactionManager txManager) : IUnitOfWorkManager
{
    private readonly Stack<IUnitOfWorkScope> _scopeStack = new();

    private readonly List<IUnitOfWorkProvider> _providers = serviceProvider
        .GetRequiredService<IEnumerable<IUnitOfWorkProvider>>()
        .ToList();

    public IUnitOfWorkScope BeginScope(
        bool isTransactional = true,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default)
    {
        var scope = new DistributedUnitOfWorkScope(
            this,
            serviceProvider,
            txManager);

        foreach (var provider in _providers)
        {
            var providerScope = provider.BeginScope(
                scope,
                isTransactional,
                isolationLevel,
                cancellationToken);

            scope.AddProviderScope(providerScope);
        }

        _scopeStack.Push(scope);
        return scope;
    }

    public IUnitOfWorkScope? Current
    {
        get
        {
            return _scopeStack.Count > 0
                ? _scopeStack.Peek()
                : null;
        }
    }

    internal void PopScope(IUnitOfWorkScope scope)
    {
        if (_scopeStack.Count == 0 || _scopeStack.Peek() != scope)
        {
            throw new InvalidOperationException("The scope to pop does not match the current scope.");
        }

        _scopeStack.Pop();
    }
}
