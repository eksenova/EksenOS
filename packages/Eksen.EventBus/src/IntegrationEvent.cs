namespace Eksen.EventBus;

public abstract class IntegrationEvent : IEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTime CreationTime { get; init; } = DateTime.UtcNow;

    public string? CorrelationId { get; init; }

    public string? SourceApp { get; init; }
}
