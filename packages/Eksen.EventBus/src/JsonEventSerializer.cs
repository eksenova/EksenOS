using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cysharp.Serialization.Json;
using Eksen.SmartEnums;
using Eksen.ValueObjects;

namespace Eksen.EventBus;

public class JsonEventSerializer : IEventSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        serializerOptions.Converters.Add(new JsonStringEnumerationConverter());
        serializerOptions.Converters.Add(new UlidJsonConverter());
        serializerOptions.Converters.Add(JsonMetadataServices.StringConverter);
        serializerOptions.Converters.Add(JsonMetadataServices.DecimalConverter);
        serializerOptions.Converters.Add(JsonMetadataServices.Int32Converter);
        serializerOptions.Converters.Add(JsonMetadataServices.Int64Converter);
        serializerOptions.Converters.Add(JsonMetadataServices.GuidConverter);
        serializerOptions.Converters.Add(JsonMetadataServices.BooleanConverter);
        serializerOptions.Converters.Add(new JsonValueObjectConverter());

        return serializerOptions;
    }

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
