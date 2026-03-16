namespace Eksen.DistributedLocks;

public interface IDistributedLockProvider
{
    Task<IDistributedLockHandle> AcquireAsync(
        string? name = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    Task<IDistributedLockHandle> TryAcquireAsync(
        string? name = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}
