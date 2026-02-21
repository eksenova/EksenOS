using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Eksen.ValueObjects;

public interface IValueObject
{
    string ToParseableString(IFormatProvider? provider = null);

    object Value { get; }

    private static readonly List<(
        Type ValueObjectType,
        Type UnderlyingValueType,
        Type ValueObjectImplementationType)> KnownValueObjectTypes = [];

    public static abstract Type GetUnderlyingValueType();

    public static void Register<TValueObject>()
        where TValueObject : IValueObject
    {
        if (!typeof(TValueObject).IsConcreteValueObject)
        {
            throw new ArgumentException($"ValueObject does not implement {nameof(IConcreteValueObject<,>)}: " +
                                        $"{typeof(TValueObject).FullName}", nameof(TValueObject));
        }

        KnownValueObjectTypes.Add((
            typeof(TValueObject),
            TValueObject.GetUnderlyingValueType(),
            typeof(TValueObject)
        ));

        TypeDescriptor.AddAttributes(
            typeof(TValueObject),
            new TypeConverterAttribute(typeof(ValueObjectTypeConverter<,,>)
                .MakeGenericType(
                    typeof(TValueObject),
                    TValueObject.GetUnderlyingValueType(),
                    typeof(TValueObject)))
        );

        TypeDescriptor.AddAttributes(
            typeof(TValueObject),
            new JsonConverterAttribute(typeof(JsonValueObjectConverter<,,>)
                .MakeGenericType(
                    typeof(TValueObject),
                    TValueObject.GetUnderlyingValueType(),
                    typeof(TValueObject)))
        );
    }

    public static void RegisterFor(JsonSerializerOptions options)
    {
        var factory = new JsonValueObjectConverter();
        var baseTypeResolver = options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver();
        options.TypeInfoResolver = new JsonValueObjectTypeInfoResolver(baseTypeResolver);

        foreach (var knownValueObjectType in KnownValueObjectTypes)
        {
            var converter = factory.CreateConverter(knownValueObjectType.ValueObjectType, options);
            options.Converters.Add(converter);
        }
    }
}

public interface IValueObject<TValueObject, out TUnderlyingValue> : IValueObject
    where TValueObject : IValueObject<TValueObject, TUnderlyingValue>
{
    new TUnderlyingValue Value { get; }
}

public interface IConcreteValueObject<out TValueObject, in TUnderlyingValue>
    where TValueObject : IValueObject<TValueObject, TUnderlyingValue>
{
    static abstract TValueObject Create(TUnderlyingValue value);

    public static abstract TValueObject Parse(
        string value,
        IFormatProvider? formatProvider = null);
}

public abstract record ValueObject<TSelf, TUnderlyingValue, TValueObjectImplementation>
    : IValueObject<TSelf, TUnderlyingValue>
    where TSelf : ValueObject<TSelf, TUnderlyingValue, TValueObjectImplementation>, IValueObject<TSelf, TUnderlyingValue>
    where TValueObjectImplementation : IConcreteValueObject<TSelf, TUnderlyingValue>
    where TUnderlyingValue : notnull
{
    [Pure]
    public static Type GetUnderlyingValueType()
    {
        return typeof(TUnderlyingValue);
    }

    [Pure]
    public static bool TryParse(
        string? value,
        IFormatProvider? formatProvider,
        [NotNullWhen(returnValue: true)] out TSelf? parsedValue)
    {
        parsedValue = null;

        try
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parsedValue = TValueObjectImplementation.Parse(value, formatProvider);
            }
        }
        catch
        {
            // ignored
        }

        return parsedValue != null;
    }

    [Pure]
    public static bool TryParse(
        string? value,
        [NotNullWhen(returnValue: true)] out TSelf? parsedValue)
    {
        return TryParse(value, formatProvider: null, out parsedValue);
    }

    [Pure]
    public static bool TryCreate(
        TUnderlyingValue? value,
        IFormatProvider? formatProvider,
        [NotNullWhen(returnValue: true)] out TSelf? createdValue)
    {
        createdValue = null;

        try
        {
            if (value != null)
            {
                createdValue = TValueObjectImplementation.Create(value);
            }
        }
        catch
        {
            // ignored
        }

        return createdValue != null;
    }

    [Pure]
    public abstract string ToParseableString(IFormatProvider? provider = null);

    public TUnderlyingValue Value { get; }

    object IValueObject.Value
    {
        get { return Value; }
    }

    protected ValueObject(TUnderlyingValue value)
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        Value = Validate(value);
    }

    public override string ToString()
    {
        return ToParseableString();
    }

    protected abstract TUnderlyingValue Validate(TUnderlyingValue value);

    public void Deconstruct(out TUnderlyingValue value)
    {
        value = Value;
    }
}

public class ValueObjectTypeConverter<TValueObject, TUnderlyingValue, TValueObjectImplementation>
    : TypeConverter
    where TValueObject : IValueObject<TValueObject, TUnderlyingValue>
    where TValueObjectImplementation : IConcreteValueObject<TValueObject, TUnderlyingValue>
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        var baseConverter = TypeDescriptor.GetConverter(typeof(TUnderlyingValue));
        return sourceType == typeof(string) || baseConverter.CanConvertFrom(context, sourceType);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        var baseConverter = TypeDescriptor.GetConverter(typeof(TUnderlyingValue));
        return destinationType == typeof(string) || baseConverter.CanConvertTo(context, destinationType);
    }

    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value)
    {
        if (value is string stringValue)
        {
            return TValueObjectImplementation.Parse(stringValue);
        }

        var baseConverter = TypeDescriptor.GetConverter(typeof(TUnderlyingValue));
        var underlyingValue = (TUnderlyingValue?)baseConverter.ConvertFrom(context, culture, value);

        return underlyingValue != null
            ? TValueObjectImplementation.Create(underlyingValue)
            : null;
    }

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType)
    {
        if (value is TValueObject valueObject)
        {
            return valueObject.Value;
        }

        var baseConverter = TypeDescriptor.GetConverter(typeof(TUnderlyingValue));
        return baseConverter.ConvertTo(context, culture, value, destinationType);
    }
}

public class JsonValueObjectTypeInfoResolver(
    IJsonTypeInfoResolver baseTypeInfoResolver
) : IJsonTypeInfoResolver
{
    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        if (!type.IsConcreteValueObject)
        {
            return baseTypeInfoResolver
                .GetTypeInfo(type, options);
        }

        var getUnderlyingTypeMethod = type.GetMethod(nameof(ValueObject<,,>.GetUnderlyingValueType),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;

        var underlyingType = (Type)getUnderlyingTypeMethod.Invoke(obj: null, [])!;

        return baseTypeInfoResolver.GetTypeInfo(
            underlyingType, options
        );
    }
}

public class JsonValueObjectConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsConcreteValueObject;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(JsonValueObjectConverter<,,>)
            .MakeGenericType(typeToConvert.BaseType!.GetGenericArguments());

        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

public class JsonValueObjectConverter<TValueObject, TUnderlyingValue, TValueObjectImplementation>
    : JsonConverter<TValueObject>
    where TValueObject : class, IValueObject<TValueObject, TUnderlyingValue>
    where TValueObjectImplementation : IConcreteValueObject<TValueObject, TUnderlyingValue>

{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(TValueObject) == typeToConvert;
    }

    public override TValueObject? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        typeToConvert = typeof(TUnderlyingValue);

        var converter = (JsonConverter<TUnderlyingValue>?)options.Converters
            .FirstOrDefault(x => x.CanConvert(typeToConvert));
        if (converter is null)
        {
            throw new JsonException(
                $"No converter found for value object with underlying value type {typeof(TUnderlyingValue).Name}");
        }

        var value = converter.Read(ref reader, typeToConvert, options);
        return value != null
            ? TValueObjectImplementation.Create(value)
            : null;
    }

    public override void Write(Utf8JsonWriter writer, TValueObject value, JsonSerializerOptions options)
    {
        var typeToConvert = typeof(TUnderlyingValue);

        var converter = (JsonConverter<TUnderlyingValue>?)options.Converters
            .FirstOrDefault(x => x.CanConvert(typeToConvert));
        if (converter is null)
        {
            throw new JsonException(
                $"No converter found for value object with underlying value type {typeof(TUnderlyingValue).Name}");
        }

        converter.Write(writer, value.Value, options);
    }
}
public static class ValueObjectExtensions
{
    extension(Type type)
    {
        public bool IsConcreteValueObject
        {
            get
            {
                return type is { IsClass: true }
                       && type
                           .GetInterfaces()
                           .Any(y =>
                               y.IsGenericType
                               && typeof(IConcreteValueObject<,>) == y.GetGenericTypeDefinition());
            }
        }
    }
}