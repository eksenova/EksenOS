namespace Eksen.EventBus;

internal class PendingEvent
{
    public required string EventTypeName { get; init; }

    public required string Payload { get; init; }

    public string? CorrelationId { get; init; }

    public string? SourceApp { get; init; }

    public string? TargetApp { get; init; }

    public required Guid EventId { get; init; }

    public Dictionary<string, string>? Headers { get; init; }
}
