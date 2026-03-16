namespace Eksen.EventBus.EntityFrameworkCore;

public class DeadLetterMessageEntity
{
    public Guid Id { get; set; }

    public Guid OriginalMessageId { get; set; }

    public string EventType { get; set; } = null!;

    public string? HandlerType { get; set; }

    public string Payload { get; set; } = null!;

    public DateTime CreationTime { get; set; }

    public DateTime FailedTime { get; set; }

    public int TotalRetryCount { get; set; }

    public string LastError { get; set; } = null!;

    public string? CorrelationId { get; set; }

    public string? SourceApp { get; set; }

    public string? TargetApp { get; set; }

    public bool IsRequeued { get; set; }

    public DateTime? RequeuedTime { get; set; }
}
