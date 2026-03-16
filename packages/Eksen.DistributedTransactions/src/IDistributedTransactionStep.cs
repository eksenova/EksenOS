namespace Eksen.DistributedTransactions;

public interface IDistributedTransactionStep
{
    string Name { get; }

    Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);

    Task CompensateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
}
