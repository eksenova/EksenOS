using System.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Eksen.UnitOfWork;

public interface IUnitOfWorkManager
{
    Task<IUnitOfWorkScope> BeginScopeAsync(
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
}

public class UnitOfWorkManager(IServiceProvider serviceProvider) : IUnitOfWorkManager
{
    private readonly Stack<IUnitOfWorkScope> _scopeStack = new();

    private readonly List<IUnitOfWorkProvider> _providers = serviceProvider
        .GetRequiredService<IEnumerable<IUnitOfWorkProvider>>()
        .ToList();

    public virtual async Task<IUnitOfWorkScope> BeginScopeAsync(
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default)
    {
        var scopes = new List<IUnitOfWorkProviderScope>();


        var rootScope = new CompositeUnitOfWorkScope(
            this,
            serviceProvider,
            scopes);

        foreach (var provider in _providers)
        {
            var scope = await provider.BeginScopeAsync(rootScope, isolationLevel, cancellationToken);
            scopes.Add(scope);
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

        foreach (var providerScope in scope.ProviderScopes)
        {
            providerScope.Provider.PopScope(providerScope);
        }
    }
}