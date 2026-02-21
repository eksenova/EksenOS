using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace Eksen.SmartEnums;

public class EnumerationTypeConverter<T> : TypeConverter where T : Enumeration<T>
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string)
               || sourceType == typeof(int)
               || base.CanConvertFrom(context, sourceType);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string)
               || destinationType == typeof(int)
               || base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value)
    {
        return value switch
        {
            string stringValue => ConvertWithParse(typeof(T), stringValue),
            _ => base.ConvertFrom(context, culture, value)
        };
    }

    private static object? ConvertWithParse(Type type, string code)
    {
        var parseMethod = type
            .GetMethod(nameof(Enumeration<>.Parse), BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)!;

        return parseMethod.Invoke(obj: null, [code]);
    }

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType)
    {
        if (value is not T enumeration)
        {
            return base.ConvertTo(context, culture, value, destinationType);
        }

        if (destinationType == typeof(string))
        {
            return enumeration.Code;
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}