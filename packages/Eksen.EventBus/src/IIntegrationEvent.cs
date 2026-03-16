namespace Eksen.EventBus;

public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTime CreationTime { get; }

    string? CorrelationId { get; }

    string? SourceApp { get; }
}
