namespace Eksen.EventBus;

public interface IEventSerializer
{
    string Serialize<TEvent>(TEvent @event) where TEvent : class, IEvent;

    object? Deserialize(string payload, Type eventType);

    TEvent? Deserialize<TEvent>(string payload) where TEvent : class, IEvent;
}
