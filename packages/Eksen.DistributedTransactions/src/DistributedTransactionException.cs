namespace Eksen.DistributedTransactions;

public sealed class DistributedTransactionException : Exception
{
    public IReadOnlyList<Exception> CompensationExceptions { get; }

    public DistributedTransactionException(
        string message,
        Exception innerException,
        IReadOnlyList<Exception> compensationExceptions)
        : base(message, innerException)
    {
        CompensationExceptions = compensationExceptions;
    }

    public DistributedTransactionException(
        string message,
        IReadOnlyList<Exception> compensationExceptions)
        : base(message)
    {
        CompensationExceptions = compensationExceptions;
    }

    public DistributedTransactionException(string message)
        : base(message)
    {
        CompensationExceptions = [];
    }

    public DistributedTransactionException(string message, Exception innerException)
        : base(message, innerException)
    {
        CompensationExceptions = [];
    }
}
