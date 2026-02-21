using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Eksen.SmartEnums;

public interface IEnumeration : IComparable
{
    public string Code { get; }


    private static readonly List<Type> KnownEnumerationTypes = [];

    public static void Register<TEnumeration>()
        where TEnumeration : Enumeration<TEnumeration>
    {
        if (!typeof(TEnumeration).IsEnumeration)
        {
            throw new ArgumentException($"Type is not an enumeration: " +
                                        $"{typeof(TEnumeration).FullName}", nameof(TEnumeration));
        }

        KnownEnumerationTypes.Add(typeof(TEnumeration));

        TypeDescriptor.AddAttributes(
            typeof(TEnumeration),
            new TypeConverterAttribute(typeof(EnumerationTypeConverter<TEnumeration>)));

        TypeDescriptor.AddAttributes(
            typeof(TEnumeration),
            new JsonConverterAttribute(typeof(JsonStringEnumerationConverter<TEnumeration>)));
    }

    public static void RegisterFor(JsonSerializerOptions options)
    {
        var factory = new JsonStringEnumerationConverter();

        var baseTypeResolver = options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver();
        options.TypeInfoResolver = new JsonEnumerationTypeInfoResolver(baseTypeResolver);

        foreach (var knownEnumerationType in KnownEnumerationTypes)
        {
            var converter = factory.CreateConverter(knownEnumerationType, options);
            options.Converters.Add(converter);
        }
    }
}