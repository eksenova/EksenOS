using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Eksen.ValueObjects;

public sealed record EksenValueObjectOptions
{
    public IReadOnlyCollection<Type> ValueObjectTypes
    {
        get { return _knownValueObjectTypes.Select(t => t.ValueObjectType).ToList().AsReadOnly(); }
    }

    private readonly List<(
        Type ValueObjectType,
        Type UnderlyingValueType,
        Type ValueObjectImplementationType)> _knownValueObjectTypes = [];

    public void AddRange(IEnumerable<Type> valueObjectTypes)
    {
        foreach (var valueObjectType in valueObjectTypes)
        {
            Add(valueObjectType);
        }
    }

    public void Add(Type valueObjectType)
    {
        var method = typeof(EksenValueObjectOptions).GetMethod(nameof(Add), Type.EmptyTypes);
        if (method is null)
        {
            throw new InvalidOperationException($"Method {nameof(Add)} not found on {nameof(EksenValueObjectOptions)}");
        }

        var genericMethod = method.MakeGenericMethod(valueObjectType);
        genericMethod.Invoke(this, parameters: null);
    }

    public void Add<TValueObject>() where TValueObject : IValueObject
    {
        if (!typeof(TValueObject).IsConcreteValueObject)
        {
            throw new ArgumentException($"ValueObject does not implement {nameof(IConcreteValueObject<,>)}: " +
                                        $"{typeof(TValueObject).FullName}", nameof(TValueObject));
        }


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

        _knownValueObjectTypes.Add((
            typeof(TValueObject),
            TValueObject.GetUnderlyingValueType(),
            typeof(TValueObject)
        ));
    }

    public void AddAssembly(Assembly assembly)
    {
        List<Type> types;

        try
        {
            types = assembly.GetTypes().ToList();
        }
        catch (ReflectionTypeLoadException typeLoadException)
        {
            types = typeLoadException.Types
                .Where(t => t != null)
                .Select(x => x!)
                .ToList();
        }

        foreach (var type in types.Where(t => t.IsConcreteValueObject))
        {
            Add(type);
        }
    }

    public void ConfigureJsonOptions(JsonSerializerOptions options)
    {
        var factory = new JsonValueObjectConverter();
        var baseTypeResolver = options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver();
        options.TypeInfoResolver = new JsonValueObjectTypeInfoResolver(baseTypeResolver);

        foreach (var knownValueObjectType in _knownValueObjectTypes)
        {
            var converter = factory.CreateConverter(knownValueObjectType.ValueObjectType, options);
            options.Converters.Add(converter);
        }
    }
}