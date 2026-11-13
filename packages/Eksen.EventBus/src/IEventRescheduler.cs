namespace Eksen.EventBus;

/// <summary>
/// Redelivers an event to one handler of this host, after a delay. Transports use it for delayed retries and the
/// dead-letter store uses it to requeue a dead letter to the handler that failed (and only to that handler).
/// </summary>
public interface IEventRescheduler
{
    Task RescheduleAsync(
        EventRedelivery redelivery,
        TimeSpan delay,
        CancellationToken cancellationToken = default);
}

public sealed class EventRedelivery
{
    public required string EventTypeName { get; init; }

    public required string Payload { get; init; }

    public required Guid EventId { get; init; }

    /// <summary>
    /// The <see cref="EventHandlerDescriptor.HandlerTypeName"/> of the handler of this host that receives the event.
    /// </summary>
    public required string HandlerTypeName { get; init; }

    public string? CorrelationId { get; init; }

    public string? SourceApp { get; init; }

    public string? TargetApp { get; init; }

    /// <summary>
    /// The delivery attempt the redelivered message carries (1 starts a fresh retry budget).
    /// </summary>
    public int Attempt { get; init; } = 1;

    public DateTime? FirstFailureTime { get; init; }

    public Dictionary<string, string>? Headers { get; init; }
}
