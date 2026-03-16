namespace Eksen.DistributedLocks;

public sealed class DistributedLockException : Exception
{
    public DistributedLockException(string message) : base(message)
    {
    }

    public DistributedLockException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
