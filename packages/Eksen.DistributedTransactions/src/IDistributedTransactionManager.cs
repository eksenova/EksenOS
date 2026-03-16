namespace Eksen.DistributedTransactions;

public interface IDistributedTransactionManager
{
    IDistributedTransaction Begin(string? name = null);
}
