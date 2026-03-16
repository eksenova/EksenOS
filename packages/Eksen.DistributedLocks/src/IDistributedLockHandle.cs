namespace Eksen.DistributedLocks;

public interface IDistributedLockHandle : IAsyncDisposable
{
    string Name { get; }

    bool IsAcquired { get; }
}
