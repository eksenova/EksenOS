namespace Eksen.EventBus;

public interface IEventRescheduler
{
    Task RescheduleAsync(
        string eventTypeName,
        string payload,
        string? correlationId,
        string? sourceApp,
        Guid eventId,
        TimeSpan delay,
        CancellationToken cancellationToken = default);
}
