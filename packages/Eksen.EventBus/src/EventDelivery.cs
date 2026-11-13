namespace Eksen.EventBus;

/// <summary>
/// One delivery of an event to one handler, as a transport hands it to <see cref="IEventProcessor"/>.
/// </summary>
public sealed class EventDelivery
{
    public required string EventTypeName { get; init; }

    public required string Payload { get; init; }

    public required Guid EventId { get; init; }

    /// <summary>
    /// The <see cref="EventHandlerDescriptor.HandlerTypeName"/> of the handler this delivery is for.
    /// </summary>
    public required string HandlerTypeName { get; init; }

    public string? CorrelationId { get; init; }

    public string? SourceApp { get; init; }

    public string? TargetApp { get; init; }

    /// <summary>
    /// The 1-based delivery attempt: 1 for the first delivery, incremented by each delayed broker redelivery.
    /// </summary>
    public int Attempt { get; init; } = 1;

    /// <summary>
    /// When the first attempt of this delivery failed, if one did.
    /// </summary>
    public DateTime? FirstFailureTime { get; init; }

    /// <summary>
    /// True when the transport will not redeliver this message if this attempt fails. Only then does the processor
    /// move a failing message to the dead-letter store; otherwise it rethrows and the transport redelivers later.
    /// </summary>
    public bool IsFinalAttempt { get; init; } = true;
}

public enum EventProcessingOutcome
{
    Processed,
    Duplicate,
    DeadLettered
}
