using System.ComponentModel;
using System.Globalization;

namespace Eksen.ValueObjects.Entities;

public class EntityIdStringTypeConverter<TEntityId, TValue> : TypeConverter
    where TEntityId : IEntityId<TEntityId, TValue>, IParsable<TEntityId>
    where TValue : IComparable,
    IComparable<TValue>,
    IEquatable<TValue>,
    ISpanFormattable,
    ISpanParsable<TValue>,
    IUtf8SpanFormattable
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value)
    {
        if (value is not string stringValue)
        {
            return base.ConvertFrom(context, culture, value);
        }

        return TEntityId.TryParse(stringValue, culture, out var entityId)
            ? entityId
            : throw new FormatException($"Invalid {typeof(TValue).Name}: \"{stringValue}\"");
    }
}