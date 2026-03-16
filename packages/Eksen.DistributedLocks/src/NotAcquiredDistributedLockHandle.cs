namespace Eksen.DistributedLocks;

public sealed class NotAcquiredDistributedLockHandle(string name) : IDistributedLockHandle
{
    public string Name { get; } = name;

    public bool IsAcquired => false;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
