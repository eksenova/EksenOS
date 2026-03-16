namespace Eksen.DistributedLocks;

public sealed record EksenDistributedLockOptions
{
    public TimeSpan? DefaultTimeout { get; set; }
}
