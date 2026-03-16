namespace Eksen.EventBus;

public interface IEventSerializer
{
    string Serialize<TEvent>(TEvent @event) where TEvent : class, IIntegrationEvent;

    object? Deserialize(string payload, Type eventType);

    TEvent? Deserialize<TEvent>(string payload) where TEvent : class, IIntegrationEvent;
}
