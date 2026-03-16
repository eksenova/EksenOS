namespace Eksen.DistributedTransactions;

internal sealed class DistributedTransactionManager(
    IServiceProvider serviceProvider) : IDistributedTransactionManager
{
    public IDistributedTransaction Begin(string? name = null)
    {
        name ??= Guid.NewGuid().ToString("N");
        return new DistributedTransaction(name, serviceProvider);
    }
}
