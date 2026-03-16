namespace Eksen.DistributedTransactions;

public interface IDistributedTransaction : IAsyncDisposable
{
    string Name { get; }

    DistributedTransactionState State { get; }

    IDistributedTransaction AddStep(IDistributedTransactionStep step);

    IDistributedTransaction AddStep<TStep>() where TStep : IDistributedTransactionStep;

    IDistributedTransaction AddStep(
        string name,
        Func<IServiceProvider, CancellationToken, Task> execute,
        Func<IServiceProvider, CancellationToken, Task> compensate);

    IDistributedTransaction AddStep(
        Func<IServiceProvider, CancellationToken, Task> execute,
        Func<IServiceProvider, CancellationToken, Task> compensate);

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
