namespace Eksen.EventBus.Inbox;

public enum InboxClaimStatus
{
    /// <summary>
    /// This consumer owns the event for the handler and must complete or release the claim.
    /// </summary>
    Claimed,

    /// <summary>
    /// The handler already processed the event; the delivery is a duplicate.
    /// </summary>
    AlreadyProcessed,

    /// <summary>
    /// Another consumer holds an unexpired claim; the delivery must be retried later, not acknowledged.
    /// </summary>
    InProgress
}

/// <param name="Status">The claim result.</param>
/// <param name="Message">The inbox record the claim refers to (for <see cref="InboxClaimStatus.Claimed"/>, the one to complete or release).</param>
public sealed record InboxClaim(InboxClaimStatus Status, InboxMessage? Message);

/// <summary>
/// Thrown when another consumer is still processing the same event for the same handler.
/// </summary>
public class InboxClaimConflictException(Guid eventId, string handlerType)
    : Exception($"Event {eventId} is being processed by another consumer of handler {handlerType}.")
{
    public Guid EventId { get; } = eventId;

    public string HandlerType { get; } = handlerType;
}
