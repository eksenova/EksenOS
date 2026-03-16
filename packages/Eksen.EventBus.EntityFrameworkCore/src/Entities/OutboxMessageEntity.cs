namespace Eksen.EventBus.EntityFrameworkCore;

public class OutboxMessageEntity
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = null!;

    public string Payload { get; set; } = null!;

    public DateTime CreationTime { get; set; }

    public DateTime? ProcessedTime { get; set; }

    public int Status { get; set; }

    public int RetryCount { get; set; }

    public string? LastError { get; set; }

    public string? CorrelationId { get; set; }

    public string? SourceApp { get; set; }

    public string? TargetApp { get; set; }

    public string? Headers { get; set; }
}
