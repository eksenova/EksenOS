namespace Eksen.EventBus;

public interface IEvent
{
    Guid EventId { get; }

    DateTime CreationTime { get; }

    string? CorrelationId { get; }

    string? SourceApp { get; }
}
