namespace Eksen.EventBus.RabbitMq;

/// <summary>
/// Redelivers an event to one handler queue of this app (<c>{AppName}.{HandlerType}</c>), immediately or through the
/// delay tier that covers the delay. The dead-letter stores use it to requeue a dead letter to the handler that failed.
/// </summary>
public class RabbitMqEventRescheduler(RabbitMqEventBusTransport transport) : IEventRescheduler
{
    public Task RescheduleAsync(
        EventRedelivery redelivery,
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        return transport.RescheduleAsync(redelivery, delay, cancellationToken);
    }
}
