using Microsoft.Extensions.DependencyInjection;

namespace Eksen.DistributedTransactions;

internal sealed class DistributedTransaction(
    string name,
    IServiceProvider serviceProvider) : IDistributedTransaction
{
    private readonly List<IDistributedTransactionStep> _steps = [];
    private readonly List<IDistributedTransactionStep> _executedSteps = [];
    private bool _isDisposed;

    public string Name { get; } = name;

    public DistributedTransactionState State { get; private set; } = DistributedTransactionState.Pending;

    public IDistributedTransaction AddStep(IDistributedTransactionStep step)
    {
        EnsurePending();
        _steps.Add(step);
        return this;
    }

    public IDistributedTransaction AddStep<TStep>() where TStep : IDistributedTransactionStep
    {
        var step = serviceProvider.GetRequiredService<TStep>();
        return AddStep(step);
    }

    public IDistributedTransaction AddStep(
        string name,
        Func<IServiceProvider, CancellationToken, Task> execute,
        Func<IServiceProvider, CancellationToken, Task> compensate)
    {
        return AddStep(new InlineDistributedTransactionStep(name, execute, compensate));
    }

    public IDistributedTransaction AddStep(
        Func<IServiceProvider, CancellationToken, Task> execute,
        Func<IServiceProvider, CancellationToken, Task> compensate)
    {
        return AddStep($"Step-{_steps.Count + 1}", execute, compensate);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        EnsurePending();
        State = DistributedTransactionState.Executing;

        foreach (var step in _steps)
        {
            try
            {
                await step.ExecuteAsync(serviceProvider, cancellationToken);
                _executedSteps.Add(step);
            }
            catch (Exception ex)
            {
                State = DistributedTransactionState.Compensating;

                var compensationExceptions = await CompensateExecutedStepsAsync(cancellationToken);

                State = compensationExceptions.Count > 0
                    ? DistributedTransactionState.Failed
                    : DistributedTransactionState.Compensated;

                throw new DistributedTransactionException(
                    $"Transaction '{Name}' failed at step '{step.Name}'. " +
                    $"Compensation was attempted for {_executedSteps.Count} previously executed step(s).",
                    ex,
                    compensationExceptions);
            }
        }

        State = DistributedTransactionState.Committed;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (State == DistributedTransactionState.Committed)
        {
            throw new InvalidOperationException("Cannot rollback a committed transaction.");
        }

        if (State is DistributedTransactionState.Compensated or DistributedTransactionState.Failed)
        {
            return;
        }

        State = DistributedTransactionState.Compensating;

        var compensationExceptions = await CompensateExecutedStepsAsync(cancellationToken);

        if (compensationExceptions.Count > 0)
        {
            State = DistributedTransactionState.Failed;
            throw new DistributedTransactionException(
                $"Transaction '{Name}' rollback encountered errors during compensation.",
                compensationExceptions);
        }

        State = DistributedTransactionState.Compensated;
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (State is DistributedTransactionState.Pending or DistributedTransactionState.Executing)
        {
            try
            {
                await RollbackAsync();
            }
            catch
            {
                // Best-effort rollback on dispose
            }
        }
    }

    private async Task<IReadOnlyList<Exception>> CompensateExecutedStepsAsync(
        CancellationToken cancellationToken)
    {
        var exceptions = new List<Exception>();

        for (var i = _executedSteps.Count - 1; i >= 0; i--)
        {
            try
            {
                await _executedSteps[i].CompensateAsync(serviceProvider, cancellationToken);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        return exceptions;
    }

    private void EnsurePending()
    {
        if (State != DistributedTransactionState.Pending)
        {
            throw new InvalidOperationException(
                $"Transaction '{Name}' is in state '{State}' and cannot be modified.");
        }
    }
}
