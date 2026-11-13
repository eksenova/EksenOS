using System.Diagnostics;
using Eksen.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Eksen.UnitOfWork;

public class CompositeUnitOfWorkScope : IUnitOfWorkScope
{
    private readonly Dictionary<CallbackType, List<Func<IServiceProvider, CancellationToken, Task>>> _callbacks = new();
    private readonly List<Func<IServiceProvider, CancellationToken, Task>> _postCommitActions = [];
    private readonly UnitOfWorkManager _unitOfWorkManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ICollection<IUnitOfWorkProviderScope> _scopes;
    private readonly UnitOfWorkTelemetry _telemetry;
    private readonly long _startedAt;
    private Activity? _activity;
    private string _outcome = UnitOfWorkTelemetry.DisposedUncommittedOutcome;
    private Exception? _failure;
    private bool _isDisposed;

    public CompositeUnitOfWorkScope(
        UnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider)
    {
        _unitOfWorkManager = unitOfWorkManager;
        _serviceProvider = serviceProvider;
        _scopes = new List<IUnitOfWorkProviderScope>();
        ScopeId = Guid.NewGuid();

        foreach (var type in Enum.GetValues<CallbackType>())
        {
            _callbacks[type] = [];
        }

        _telemetry = serviceProvider.GetService<UnitOfWorkTelemetry>() ?? UnitOfWorkTelemetry.Shared;
        _startedAt = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Whether the provider scopes were opened with a transaction. Set by <see cref="UnitOfWorkManager"/>.
    /// </summary>
    internal bool IsTransactional { get; set; } = true;

    /// <summary>
    /// Starts the optional <c>uow</c> span (<c>Telemetry:UnitOfWorkSpans</c>). The span is not made current:
    /// it ends inside <see cref="DisposeAsync"/>, whose change to <see cref="Activity.Current"/> would not flow back
    /// to the caller, so a current span would leak to every later span of the request.
    /// </summary>
    internal void StartTelemetry()
    {
        var spansEnabled = _serviceProvider.GetService<IOptions<EksenTelemetryOptions>>()?.Value.UnitOfWorkSpans == true;
        if (!spansEnabled || !UnitOfWorkTelemetry.ActivitySource.HasListeners())
        {
            return;
        }

        var previous = Activity.Current;
        _activity = UnitOfWorkTelemetry.ActivitySource.StartActivity(
            "uow",
            ActivityKind.Internal,
            parentContext: default,
            tags:
            [
                new KeyValuePair<string, object?>(EksenSemanticConventions.EksenUowTransactional, IsTransactional),
                new KeyValuePair<string, object?>("eksen.uow.scope_id", ScopeId.ToString())
            ]);
        Activity.Current = previous;
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
        try
        {
            try
            {
                await AlertCallbacksAsync(CallbackType.Completing, cancellationToken);
                foreach (var scope in _scopes)
                {
                    await scope.CommitAsync(cancellationToken);
                }
            }
            catch (Exception exception)
            {
                // The provider scopes roll themselves back when their commit fails.
                MarkEnded(UnitOfWorkTelemetry.RollbackOutcome, "uow.rollback", exception);
                throw;
            }

            MarkEnded(UnitOfWorkTelemetry.CommitOutcome, "uow.commit", failure: null);

            await AlertCallbacksAsync(CallbackType.Completed, cancellationToken);
            foreach (var action in _postCommitActions)
            {
                try
                {
                    await action(_serviceProvider, cancellationToken);
                }
                catch (Exception exception)
                {
                    _telemetry.PostCommitFailures.Add(
                        1,
                        new KeyValuePair<string, object?>(
                            EksenSemanticConventions.ErrorType,
                            exception.GetType().FullName ?? exception.GetType().Name));
                    throw;
                }
            }
        }
        finally
        {
            await DisposeAsync();
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            MarkEnded(UnitOfWorkTelemetry.RollbackOutcome, "uow.rollback", failure: null);

            foreach (var scope in _scopes)
            {
                await scope.RollbackAsync(cancellationToken);
            }

            await AlertCallbacksAsync(CallbackType.Rollback, cancellationToken);
        }
        finally
        {
            await DisposeAsync();
        }
    }

    private void MarkEnded(string outcome, string eventName, Exception? failure)
    {
        if (_isDisposed || _outcome != UnitOfWorkTelemetry.DisposedUncommittedOutcome)
        {
            return;
        }

        _outcome = outcome;
        _failure = failure;

        var tags = new ActivityTagsCollection
        {
            { EksenSemanticConventions.EksenUowTransactional, IsTransactional }
        };
        if (failure is not null)
        {
            tags.Add(EksenSemanticConventions.ErrorType, failure.GetType().FullName ?? failure.GetType().Name);
        }

        var activityEvent = new ActivityEvent(eventName, tags: tags);
        Activity.Current?.AddEvent(activityEvent);
        if (_activity is not null && !ReferenceEquals(_activity, Activity.Current))
        {
            _activity.AddEvent(activityEvent);
        }
    }

    private void RecordEnd()
    {
        var tags = new TagList
        {
            { EksenSemanticConventions.EksenUowOutcome, _outcome },
            { EksenSemanticConventions.EksenUowTransactional, IsTransactional }
        };
        if (_failure is not null)
        {
            tags.Add(EksenSemanticConventions.ErrorType, _failure.GetType().FullName ?? _failure.GetType().Name);
        }

        _telemetry.Duration.Record(Stopwatch.GetElapsedTime(_startedAt).TotalSeconds, tags);

        if (_activity is null)
        {
            return;
        }

        _activity.SetTag(EksenSemanticConventions.EksenUowOutcome, _outcome);
        if (_failure is not null)
        {
            _activity.RecordException(_failure);
        }

        var previous = Activity.Current;
        _activity.Stop();
        Activity.Current = previous;
        _activity = null;
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

    public virtual void AddPostCommitAction(Func<IServiceProvider, CancellationToken, Task> action)
    {
        _postCommitActions.Add(action);
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        List<Exception> errors = [];
        foreach (var scope in _scopes)
        {
            try
            {
                await scope.DisposeAsync();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        _unitOfWorkManager.PopScope(this);
        _isDisposed = true;
        RecordEnd();

        if (errors.Count > 0)
        {
            throw new AggregateException("Unit of work cleanup failed.", errors);
        }
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