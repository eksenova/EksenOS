namespace Eksen.EventBus;

public class EksenEventBusOptions
{
    public string AppName { get; set; } = "Default";

    public OutboxOptions Outbox { get; set; } = new();

    public InboxOptions Inbox { get; set; } = new();

    public RetryOptions Retry { get; set; } = new();

    public DeadLetterOptions DeadLetter { get; set; } = new();
}

public class OutboxOptions
{
    public bool IsEnabled { get; set; } = true;

    public int BatchSize { get; set; } = 100;

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);
}

public class InboxOptions
{
    public bool IsEnabled { get; set; } = true;

    public TimeSpan IdempotencyWindow { get; set; } = TimeSpan.FromDays(7);
}

public class RetryOptions
{
    public int MaxRetryAttempts { get; set; } = 3;

    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    public double BackoffMultiplier { get; set; } = 2.0;
}

public class DeadLetterOptions
{
    public bool IsEnabled { get; set; } = true;

    public int MaxRetryAttemptsBeforeDeadLetter { get; set; } = 5;
}
