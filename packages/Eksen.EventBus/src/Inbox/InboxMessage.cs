namespace Eksen.EventBus.Inbox;

public class InboxMessage
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public string EventType { get; set; } = null!;

    public string HandlerType { get; set; } = null!;

    public string Payload { get; set; } = null!;

    public DateTime CreationTime { get; set; }

    public DateTime? ProcessedTime { get; set; }

    public InboxMessageStatus Status { get; set; }

    public int RetryCount { get; set; }

    public string? LastError { get; set; }

    public string? CorrelationId { get; set; }

    public string? SourceApp { get; set; }
}
