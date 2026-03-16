using Eksen.UnitOfWork;

namespace Eksen.DistributedTransactions;

internal sealed class DistributedUnitOfWorkScope : IUnitOfWorkScope
{
    private readonly Dictionary<CallbackType, List<Func<IServiceProvider, CancellationToken, Task>>> _callbacks = new();
    private readonly List<Func<IServiceProvider, CancellationToken, Task>> _postCommitActions = [];
    private readonly DistributedUnitOfWorkManager _manager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDistributedTransactionManager _txManager;
    private readonly ICollection<IUnitOfWorkProviderScope> _scopes;
    private bool _isDisposed;

    public DistributedUnitOfWorkScope(
        DistributedUnitOfWorkManager manager,
        IServiceProvider serviceProvider,
        IDistributedTransactionManager txManager)
    {
        _manager = manager;
        _serviceProvider = serviceProvider;
        _txManager = txManager;
        _scopes = new List<IUnitOfWorkProviderScope>();
        ScopeId = Guid.NewGuid();

        foreach (var type in Enum.GetValues<CallbackType>())
        {
            _callbacks[type] = [];
        }
    }

    public Guid ScopeId { get; }

    public IReadOnlyCollection<IUnitOfWorkProviderScope> ProviderScopes
    {
        get
        {
            return _scopes.ToList().AsReadOnly();
        }
    }

    public void AddProviderScope(IUnitOfWorkProviderScope scope)
    {
        _scopes.Add(scope);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await AlertCallbacksAsync(CallbackType.Completing, cancellationToken);

        var tx = _txManager.Begin($"UoW-{ScopeId:N}");

        foreach (var providerScope in _scopes)
        {
            var scope = providerScope;
            tx.AddStep(
                $"Commit-{scope.Provider.GetType().Name}",
                async (_, ct) => await scope.CommitAsync(ct),
                async (_, ct) => await scope.RollbackAsync(ct));
        }

        await tx.CommitAsync(cancellationToken);

        await AlertCallbacksAsync(CallbackType.Completed, cancellationToken);

        foreach (var action in _postCommitActions)
        {
            await action(_serviceProvider, cancellationToken);
        }

        await DisposeAsync();
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        foreach (var scope in _scopes)
        {
            await scope.RollbackAsync(cancellationToken);
        }

        await AlertCallbacksAsync(CallbackType.Rollback, cancellationToken);
        await DisposeAsync();
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var scope in _scopes)
        {
            await scope.SaveChangesAsync(cancellationToken);
        }
    }

    public void AddRollbackCallback(Func<IServiceProvider, CancellationToken, Task> callback)
    {
        _callbacks[CallbackType.Rollback].Add(callback);
    }

    public void AddCompletingCallback(Func<IServiceProvider, CancellationToken, Task> callback)
    {
        _callbacks[CallbackType.Completing].Add(callback);
    }

    public void AddCompletedCallback(Func<IServiceProvider, CancellationToken, Task> callback)
    {
        _callbacks[CallbackType.Completed].Add(callback);
    }

    public void AddPostCommitAction(Func<IServiceProvider, CancellationToken, Task> action)
    {
        _postCommitActions.Add(action);
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        foreach (var scope in _scopes)
        {
            await scope.DisposeAsync();
        }

        _manager.PopScope(this);

        _isDisposed = true;
    }

    private async Task AlertCallbacksAsync(
        CallbackType callbackType,
        CancellationToken cancellationToken = default)
    {
        foreach (var callback in _callbacks[callbackType])
        {
            await callback(_serviceProvider, cancellationToken);
        }
    }

    private enum CallbackType
    {
        Rollback,
        Completing,
        Completed
    }
}
