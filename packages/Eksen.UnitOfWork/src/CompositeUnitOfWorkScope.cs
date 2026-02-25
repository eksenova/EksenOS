namespace Eksen.UnitOfWork;

public class CompositeUnitOfWorkScope : IUnitOfWorkScope
{
    private readonly Dictionary<CallbackType, List<Func<IServiceProvider, CancellationToken, Task>>> _callbacks = new();
    private readonly UnitOfWorkManager _unitOfWorkManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ICollection<IUnitOfWorkProviderScope> _scopes;

    public CompositeUnitOfWorkScope(
        UnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider)
    {
        _unitOfWorkManager = unitOfWorkManager;
        _serviceProvider = serviceProvider;
        _scopes = new List<IUnitOfWorkProviderScope>();

        foreach (var type in Enum.GetValues<CallbackType>())
        {
            _callbacks[type] = [];
        }
    }

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

        foreach (var scope in _scopes)
        {
            await scope.CommitAsync(cancellationToken);
        }

        await AlertCallbacksAsync(CallbackType.Completed, cancellationToken);
        _unitOfWorkManager.PopScope(this);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        foreach (var scope in _scopes)
        {
            await scope.RollbackAsync(cancellationToken);
        }

        await AlertCallbacksAsync(CallbackType.Rollback, cancellationToken);
        _unitOfWorkManager.PopScope(this);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var scope in _scopes)
        {
            await scope.SaveChangesAsync(cancellationToken);
        }
    }

    public virtual void AddRollbackCallback(Func<IServiceProvider, CancellationToken, Task> callback)
    {
        _callbacks[CallbackType.Rollback].Add(callback);
    }

    public virtual void AddCompletingCallback(Func<IServiceProvider, CancellationToken, Task> callback)
    {
        _callbacks[CallbackType.Completing].Add(callback);
    }

    public virtual void AddCompletedCallback(Func<IServiceProvider, CancellationToken, Task> callback)
    {
        _callbacks[CallbackType.Completed].Add(callback);
    }

    public virtual async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            await scope.DisposeAsync();
        }

        _unitOfWorkManager.PopScope(this);
    }

    protected virtual async Task AlertCallbacksAsync(
        CallbackType callbackType,
        CancellationToken cancellationToken = default)
    {
        foreach (var callback in _callbacks[callbackType])
        {
            await callback(_serviceProvider, cancellationToken);
        }
    }

    protected enum CallbackType
    {
        Rollback,
        Completing,
        Completed
    }
}