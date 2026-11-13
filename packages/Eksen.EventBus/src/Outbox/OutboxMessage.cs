namespace Eksen.EventBus.Outbox;

public class OutboxMessage
{
    public Guid Id { get; set; }

    /// <summary>
    /// The <see cref="IEvent.EventId"/> of the stored event; it becomes the broker message id, which consumers
    /// deduplicate on.
    /// </summary>
    public Guid EventId { get; set; }

    public string EventType { get; set; } = null!;

    public string Payload { get; set; } = null!;

    public DateTime CreationTime { get; set; }

    public DateTime? ProcessedTime { get; set; }

    public OutboxMessageStatus Status { get; set; }

    public int RetryCount { get; set; }

    public string? LastError { get; set; }

    public string? CorrelationId { get; set; }

    public string? SourceApp { get; set; }

    public string? TargetApp { get; set; }

    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// The earliest time the row may be published again after a failed attempt; null when due immediately.
    /// </summary>
    public DateTime? NextAttemptTime { get; set; }

    /// <summary>
    /// The lease of the publisher that currently owns the row, if any.
    /// </summary>
    public Guid? LeaseToken { get; set; }

    public DateTime? LeaseExpiresAt { get; set; }
}
