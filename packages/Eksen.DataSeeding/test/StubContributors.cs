namespace Eksen.DataSeeding.Tests;

public class StubContributorA : IDataSeedContributor
{
    public List<string> Calls { get; } = [];

    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add(nameof(StubContributorA));
        return Task.CompletedTask;
    }
}

public class StubContributorB : IDataSeedContributor
{
    public List<string> Calls { get; } = [];

    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add(nameof(StubContributorB));
        return Task.CompletedTask;
    }
}

[SeedAfter(typeof(StubContributorA))]
public class StubContributorAfterA : IDataSeedContributor
{
    public List<string> Calls { get; } = [];

    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add(nameof(StubContributorAfterA));
        return Task.CompletedTask;
    }
}

[SeedAfter(typeof(StubContributorA))]
[SeedAfter(typeof(StubContributorB))]
public class StubContributorAfterAAndB : IDataSeedContributor
{
    public List<string> Calls { get; } = [];

    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add(nameof(StubContributorAfterAAndB));
        return Task.CompletedTask;
    }
}

public abstract class AbstractContributor : IDataSeedContributor
{
    public abstract Task SeedAsync(CancellationToken cancellationToken = default);
}

public class DisposableContributor : IDataSeedContributor, IDisposable
{
    public bool IsDisposed { get; private set; }

    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public class AsyncDisposableContributor : IDataSeedContributor, IAsyncDisposable
{
    public bool IsDisposed { get; private set; }

    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}

[SeedAfter(typeof(MissingSeedContributor))]
public class ContributorWithMissingSeedAfter : IDataSeedContributor
{
    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class MissingSeedContributor : IDataSeedContributor
{
    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class DisposableTracker
{
    public bool DisposeCalled { get; set; }
    public bool AsyncDisposeCalled { get; set; }
}

public class TrackingDisposableContributor(DisposableTracker tracker) : IDataSeedContributor, IDisposable
{
    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        tracker.DisposeCalled = true;
    }
}

public class TrackingAsyncDisposableContributor(DisposableTracker tracker) : IDataSeedContributor, IAsyncDisposable
{
    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        tracker.AsyncDisposeCalled = true;
        return ValueTask.CompletedTask;
    }
}
