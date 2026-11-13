namespace Eksen.EventBus.RabbitMq;

/// <summary>
/// Inspects and replays the broker dead-letter queues (<c>{queue}.dlq</c>) of this app's handler queues. This is the
/// dead-letter destination of a host without a dead-letter store.
/// </summary>
public interface IDeadLetterQueueReplayer
{
    /// <summary>
    /// The dead-letter queue of every handler of this app, with its current message count.
    /// </summary>
    Task<IReadOnlyList<DeadLetterQueueInfo>> GetQueuesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads up to <paramref name="take"/> messages from the dead-letter queue of <paramref name="handlerQueueName"/>
    /// without removing them.
    /// </summary>
    Task<IReadOnlyList<DeadLetteredMessage>> PeekAsync(
        string handlerQueueName,
        int take = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves up to <paramref name="maxMessages"/> messages from the dead-letter queue of
    /// <paramref name="handlerQueueName"/> back to that handler queue, with a fresh retry budget. Returns how many moved.
    /// </summary>
    Task<int> ReplayAsync(
        string handlerQueueName,
        int maxMessages = 100,
        CancellationToken cancellationToken = default);
}

public sealed record DeadLetterQueueInfo(
    string HandlerQueueName,
    string DeadLetterQueueName,
    string EventTypeName,
    string HandlerTypeName,
    uint MessageCount);

public sealed class DeadLetteredMessage
{
    public string? MessageId { get; init; }

    public string? EventType { get; init; }

    public required string Payload { get; init; }

    public string? Reason { get; init; }

    public string? ExceptionType { get; init; }

    public string? ExceptionMessage { get; init; }

    public int? Attempts { get; init; }

    public string? OriginalRoutingKey { get; init; }

    public DateTime? FirstFailureTime { get; init; }

    public DateTime? FailedTime { get; init; }

    public string? CorrelationId { get; init; }

    public string? SourceApp { get; init; }
}
