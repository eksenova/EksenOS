using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Eksen.SmartEnums;

public class JsonEnumerationTypeInfoResolver(
    IJsonTypeInfoResolver baseTypeInfoResolver
) : IJsonTypeInfoResolver
{
    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var result = baseTypeInfoResolver
            .GetTypeInfo(
                type, options
            );

        if (result == null)
        {
            return null;
        }

        if (result.Kind != JsonTypeInfoKind.Object)
        {
            return result;
        }

        foreach (var child in result.Properties.ToList())
        {
            if (!child.PropertyType.IsEnumeration)
            {
                continue;
            }

            var parseMethod = child.PropertyType.GetMethod(nameof(Enumeration<>.Parse),
                BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)!;

            var newChild = result.CreateJsonPropertyInfo(typeof(string), child.Name);
            newChild.Get = child.Get != null
                ? obj =>
                {
                    var enumValue = (IEnumeration?)child.Get?.Invoke(obj);
                    return enumValue?.Code;
                }
                : null;

            newChild.Set = child.Set != null
                ? (obj, value) =>
                {
                    var code = (string)value!;
                    var enumValue = parseMethod.Invoke(obj: null, [code]);
                    child.Set?.Invoke(obj, enumValue);
                }
                : null;

            newChild.ObjectCreationHandling = child.ObjectCreationHandling;
            newChild.AttributeProvider = child.AttributeProvider;
            newChild.CustomConverter = child.CustomConverter;
            newChild.IsExtensionData = child.IsExtensionData;
            newChild.IsGetNullable = child.IsGetNullable;
            newChild.IsSetNullable = child.IsSetNullable;
            newChild.IsRequired = child.IsRequired;
            newChild.NumberHandling = child.NumberHandling;
            newChild.ShouldSerialize = child.ShouldSerialize;
            newChild.Order = child.Order;
            newChild.AttributeProvider = child.PropertyType;

            var index = result.Properties.IndexOf(child);

            result.Properties.Remove(child);
            result.Properties.Insert(index, newChild);
        }

        return result;
    }
}