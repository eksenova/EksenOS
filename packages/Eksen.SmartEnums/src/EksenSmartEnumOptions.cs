using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Eksen.SmartEnums;

public sealed class EksenSmartEnumOptions
{
    public IReadOnlyCollection<Type> EnumerationTypes
    {
        get { return _knownEnumerationTypes.AsReadOnly(); }
    }

    private readonly HashSet<Type> _knownEnumerationTypes = [];

    public void AddRange(IEnumerable<Type> enumerationTypes)
    {
        foreach (var enumerationType in enumerationTypes)
        {
            Add(enumerationType);
        }
    }

    public void Add(Type enumerationType)
    {
        var method = typeof(EksenSmartEnumOptions)
            .GetMethod(nameof(Add), Type.EmptyTypes)!
            .MakeGenericMethod(enumerationType);
        method.Invoke(this, parameters: null);
    }

    public void Add<TEnumeration>()
        where TEnumeration : Enumeration<TEnumeration>
    {
        if (!typeof(TEnumeration).IsEnumeration)
        {
            throw new ArgumentException($"Type is not an enumeration: " +
                                        $"{typeof(TEnumeration).FullName}", nameof(TEnumeration));
        }

        if (!_knownEnumerationTypes.Add(typeof(TEnumeration)))
        {
            return;
        }

        TypeDescriptor.AddAttributes(
            typeof(TEnumeration),
            new TypeConverterAttribute(typeof(SmartEnumTypeConverter<TEnumeration>)));

        TypeDescriptor.AddAttributes(
            typeof(TEnumeration),
            new JsonConverterAttribute(typeof(JsonStringSmartEnumConverter<TEnumeration>)));
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

        foreach (var type in types.Where(t => t.IsEnumeration))
        {
            Add(type);
        }
    }

    public void ConfigureJsonOptions(JsonSerializerOptions options)
    {
        var factory = new JsonStringEnumerationConverter();

        var baseTypeResolver = options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver();
        options.TypeInfoResolver = new JsonSmartEnumTypeInfoResolver(baseTypeResolver);

        foreach (var knownEnumerationType in _knownEnumerationTypes)
        {
            var converter = factory.CreateConverter(knownEnumerationType, options);
            options.Converters.Add(converter);
        }
    }
}