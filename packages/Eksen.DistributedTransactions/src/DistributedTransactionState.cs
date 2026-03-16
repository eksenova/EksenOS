namespace Eksen.DistributedTransactions;

public enum DistributedTransactionState
{
    Pending,
    Executing,
    Committed,
    Compensating,
    Compensated,
    Failed
}
