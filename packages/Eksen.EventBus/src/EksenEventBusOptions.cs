namespace Eksen.EventBus;

public class EksenEventBusOptions
{
    /// <summary>
    /// The logical name of this host. Consumer queues are named <c>{AppName}.{HandlerType}</c> and a publish with
    /// <see cref="PublishOptions.TargetApp"/> reaches only the queues of the app with that name.
    /// </summary>
    public string AppName { get; set; } = "Default";

    public OutboxOptions Outbox { get; set; } = new();

    public InboxOptions Inbox { get; set; } = new();

    public RetryOptions Retry { get; set; } = new();

    public RedeliveryOptions Redelivery { get; set; } = new();

    public DeadLetterOptions DeadLetter { get; set; } = new();

    public CleanupOptions Cleanup { get; set; } = new();
}

public class OutboxOptions
{
    /// <summary>
    /// Writes published integration events to the outbox store. Without a registered <c>IOutboxStore</c> the bus
    /// publishes directly to the transport and the outbox processor does not run.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    public int BatchSize { get; set; } = 100;

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long a host owns the rows it claimed. A row whose lease expired (its host died mid-batch) is claimable
    /// again.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Publish attempts before a row is marked <c>Failed</c>.
    /// </summary>
    public int MaxPublishAttempts { get; set; } = 10;

    /// <summary>
    /// Delay before the second publish attempt; later attempts back off exponentially with jitter.
    /// </summary>
    public TimeSpan RetryInitialDelay { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(10);

    public double RetryBackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Published rows older than this are purged by the cleanup service.
    /// </summary>
    public TimeSpan ProcessedRetention { get; set; } = TimeSpan.FromDays(7);
}

public class InboxOptions
{
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// How long a processed event is remembered for deduplication. Processed inbox rows older than this are purged
    /// by the cleanup service; the Redis inbox keeps its processed marker for exactly this long.
    /// </summary>
    public TimeSpan IdempotencyWindow { get; set; } = TimeSpan.FromDays(7);
}

/// <summary>
/// The short in-process retries of a failing handler, before the message is handed back to the transport.
/// </summary>
public class RetryOptions
{
    public int MaxRetryAttempts { get; set; } = 3;

    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    public double BackoffMultiplier { get; set; } = 2.0;
}

/// <summary>
/// Delayed redeliveries through the broker after the in-process retries are exhausted. Attempt <c>n</c> waits
/// <c>InitialDelay * BackoffMultiplier^(n-1)</c>, capped at <see cref="MaxDelay"/>, with +/- <see cref="JitterFactor"/>.
/// The number of redeliveries is <see cref="DeadLetterOptions.MaxRetryAttemptsBeforeDeadLetter"/>.
/// </summary>
public class RedeliveryOptions
{
    public bool IsEnabled { get; set; } = true;

    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(15);

    public double BackoffMultiplier { get; set; } = 4.0;

    public double JitterFactor { get; set; } = 0.2;
}

public class DeadLetterOptions
{
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Delayed broker redeliveries of a failing message before it is dead-lettered.
    /// </summary>
    public int MaxRetryAttemptsBeforeDeadLetter { get; set; } = 5;
}

public class CleanupOptions
{
    /// <summary>
    /// Periodically purges processed outbox rows older than <see cref="OutboxOptions.ProcessedRetention"/> and
    /// processed inbox rows older than <see cref="InboxOptions.IdempotencyWindow"/>.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    public int BatchSize { get; set; } = 1000;
}
