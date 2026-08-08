namespace Eksen.EventBus;

public interface ILocalEventHandler<in TEvent> : IEventHandler<TEvent>
    where TEvent : class, ILocalEvent
{
}
