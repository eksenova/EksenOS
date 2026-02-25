using System.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Eksen.UnitOfWork;

public interface IUnitOfWorkManager
{
    IUnitOfWorkScope BeginScope(
        bool isTransactional = true,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default);

    IUnitOfWorkScope? Current { get; }
}

public interface IUnitOfWorkProviderScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    IUnitOfWorkProvider Provider { get; }

    IUnitOfWorkScope ParentScope { get; }
}

public interface IUnitOfWorkScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    void AddRollbackCallback(Func<IServiceProvider, CancellationToken, Task> callback);

    void AddCompletingCallback(Func<IServiceProvider, CancellationToken, Task> callback);

    void AddCompletedCallback(Func<IServiceProvider, CancellationToken, Task> callback);

    IReadOnlyCollection<IUnitOfWorkProviderScope> ProviderScopes { get; }

    void AddProviderScope(IUnitOfWorkProviderScope scope);
}

public class UnitOfWorkManager(IServiceProvider serviceProvider) : IUnitOfWorkManager
{
    private readonly Stack<IUnitOfWorkScope> _scopeStack = new();

    private readonly List<IUnitOfWorkProvider> _providers = serviceProvider
        .GetRequiredService<IEnumerable<IUnitOfWorkProvider>>()
        .ToList();

    public virtual IUnitOfWorkScope BeginScope(
        bool isTransactional = true,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default)
    {
        var rootScope = new CompositeUnitOfWorkScope(
            this,
            serviceProvider);

        foreach (var provider in _providers)
        {
            var providerScope = provider.BeginScope(
                rootScope,
                isTransactional,
                isolationLevel,
                cancellationToken
            );

            rootScope.AddProviderScope(providerScope);
        }

        _scopeStack.Push(rootScope);
        return rootScope;
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

    protected internal virtual void PopScope(IUnitOfWorkScope scope)
    {
        if (_scopeStack.Count == 0 || _scopeStack.Peek() != scope)
        {
            throw new InvalidOperationException(message: "The scope to pop does not match the current scope.");
        }

        _scopeStack.Pop();
    }
}