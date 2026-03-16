using System.Text.Json;

namespace Eksen.EventBus;

public class JsonEventSerializer : IEventSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public string Serialize<TEvent>(TEvent @event) where TEvent : class, IIntegrationEvent
    {
        return JsonSerializer.Serialize(@event, @event.GetType(), SerializerOptions);
    }

    public object? Deserialize(string payload, Type eventType)
    {
        return JsonSerializer.Deserialize(payload, eventType, SerializerOptions);
    }

    public TEvent? Deserialize<TEvent>(string payload) where TEvent : class, IIntegrationEvent
    {
        return JsonSerializer.Deserialize<TEvent>(payload, SerializerOptions);
    }
}
