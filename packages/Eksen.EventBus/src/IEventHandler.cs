namespace Eksen.EventBus;

public interface IEventHandler<in TEvent> where TEvent : class, IIntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
