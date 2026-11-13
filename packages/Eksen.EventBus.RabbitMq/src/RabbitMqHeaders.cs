namespace Eksen.EventBus.RabbitMq;

/// <summary>
/// The AMQP headers the RabbitMQ transport reads and writes.
/// </summary>
public static class RabbitMqHeaders
{
    public const string EventType = "x-event-type";
    public const string SourceApp = "x-source-app";
    public const string TargetApp = "x-target-app";

    /// <summary>
    /// The 1-based delivery attempt of the message; absent on the first delivery.
    /// </summary>
    public const string Attempt = "x-eksen-attempt";

    public const string FirstFailureTime = "x-eksen-first-failure-time";
    public const string OriginalRoutingKey = "x-eksen-original-routing-key";
    /// <summary>
    /// Selects the delay tier queue. Headers-exchange bindings ignore headers that start with <c>x-</c>, so this one
    /// does not.
    /// </summary>
    public const string DelayTier = "eksen-delay-tier";
    public const string DeadLetterReason = "x-eksen-dead-letter-reason";
    public const string ExceptionType = "x-eksen-exception-type";
    public const string ExceptionMessage = "x-eksen-exception-message";
    public const string Attempts = "x-eksen-attempts";
    public const string FailedTime = "x-eksen-failed-time";
    public const string Queue = "x-eksen-queue";
    public const string Handler = "x-eksen-handler";
    public const string Death = "x-death";

    /// <summary>
    /// Headers that describe one failure or one retry and are dropped when a message is replayed.
    /// </summary>
    internal static readonly string[] FailureHeaders =
    [
        Attempt,
        FirstFailureTime,
        DelayTier,
        DeadLetterReason,
        ExceptionType,
        ExceptionMessage,
        Attempts,
        FailedTime,
        Death,
        "x-first-death-exchange",
        "x-first-death-queue",
        "x-first-death-reason",
        "x-last-death-exchange",
        "x-last-death-queue",
        "x-last-death-reason"
    ];
}

public static class RabbitMqDeadLetterReasons
{
    public const string Poison = "poison";
    public const string MaxAttemptsExceeded = "max_attempts_exceeded";
    public const string Failed = "failed";
    public const string RedeliveryFailed = "redelivery_failed";
}
