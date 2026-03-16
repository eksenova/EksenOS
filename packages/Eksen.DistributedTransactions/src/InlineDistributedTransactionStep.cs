namespace Eksen.DistributedTransactions;

internal sealed class InlineDistributedTransactionStep(
    string name,
    Func<IServiceProvider, CancellationToken, Task> execute,
    Func<IServiceProvider, CancellationToken, Task> compensate) : IDistributedTransactionStep
{
    public string Name { get; } = name;

    public Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        => execute(serviceProvider, cancellationToken);

    public Task CompensateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        => compensate(serviceProvider, cancellationToken);
}
