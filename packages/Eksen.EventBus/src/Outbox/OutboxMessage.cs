namespace Eksen.EventBus.Outbox;

public class OutboxMessage
{
    public Guid Id { get; set; }

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
}
