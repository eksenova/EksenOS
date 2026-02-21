using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Eksen.Core;
using Eksen.Core.ErrorHandling;

namespace Eksen.SmartEnums;

public class JsonStringEnumerationConverter<T> : JsonConverter<T> where T : Enumeration<T>
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var code = reader.GetString()!;
        var parseMethod = typeof(T)
            .GetMethod(nameof(Enumeration<>.Parse), BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)!;

        try
        {
            return (T)parseMethod.Invoke(null!, [code])!;
        }
        catch (EksenException e) when (e.ErrorType == ErrorType.NotFound)
        {
            throw new JsonException($"Invalid {typeof(T).Name} code: \"{code}\"");
        }
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Code);
    }
}

public class JsonStringEnumerationConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsEnumeration;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(JsonStringEnumerationConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}