using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eksen.Telemetry.Background;

/// <summary>
/// Refreshes a value in the background so observable gauge callbacks can read it without querying a database
/// on the metrics collection path. Register it as a hosted service and read <see cref="Value"/> from the gauge.
/// </summary>
public class CachedGaugeSampler<T> : BackgroundService
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(15);

    private readonly Func<CancellationToken, Task<T>> _refresh;
    private readonly ILogger _logger;
    private readonly string _name;
    private Snapshot? _snapshot;

    public CachedGaugeSampler(
        string name,
        Func<CancellationToken, Task<T>> refresh,
        TimeSpan? interval = null,
        ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(refresh);

        var effectiveInterval = interval ?? DefaultInterval;
        if (effectiveInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "The refresh interval must be positive.");
        }

        _name = name;
        _refresh = refresh;
        Interval = effectiveInterval;
        _logger = logger ?? NullLogger.Instance;
    }

    public TimeSpan Interval { get; }

    public bool HasValue
    {
        get { return Volatile.Read(ref _snapshot) is not null; }
    }

    /// <summary>
    /// The last successfully refreshed value, or <see langword="default"/> before the first refresh.
    /// </summary>
    public T? Value
    {
        get
        {
            var snapshot = Volatile.Read(ref _snapshot);
            return snapshot is null ? default : snapshot.Value;
        }
    }

    public DateTimeOffset? LastRefreshedAt
    {
        get { return Volatile.Read(ref _snapshot)?.RefreshedAt; }
    }

    public bool TryGetValue(out T? value)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot is null)
        {
            value = default;
            return false;
        }

        value = snapshot.Value;
        return true;
    }

    /// <summary>
    /// Refreshes the cached value once. Failures are logged and keep the previous value.
    /// </summary>
    public async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var value = await _refresh(cancellationToken);
            Volatile.Write(ref _snapshot, new Snapshot(value, DateTimeOffset.UtcNow));
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Refreshing cached gauge {eksen.gauge.name} failed", _name);
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            do
            {
                await RefreshAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private sealed record Snapshot(T Value, DateTimeOffset RefreshedAt);
}
