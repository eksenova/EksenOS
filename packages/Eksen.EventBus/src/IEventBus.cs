namespace Eksen.EventBus;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;

    Task PublishAsync<TEvent>(
        TEvent @event,
        PublishOptions? options,
        CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;
}
