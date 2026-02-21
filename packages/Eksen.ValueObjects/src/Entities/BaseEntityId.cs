using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Eksen.ValueObjects.Entities;

public abstract record BaseEntityId<TSelf, TUnderlyingValue, TValueInitializer>(TUnderlyingValue Value)
    : IEntityId<TSelf, TUnderlyingValue>
    where TSelf :
    BaseEntityId<TSelf, TUnderlyingValue, TValueInitializer>,
    IComparable<TUnderlyingValue>,
    IEquatable<TUnderlyingValue>
    where TUnderlyingValue :
    IComparable,
    IComparable<TUnderlyingValue>,
    IEquatable<TUnderlyingValue>,
    ISpanFormattable,
    ISpanParsable<TUnderlyingValue>,
    IUtf8SpanFormattable
    where TValueInitializer : IEntityIdValueInitializer<TUnderlyingValue>
{
    public static TSelf NewId()
    {
        return CreateInternal(TValueInitializer.New());
    }

    public static TSelf Empty
    {
        get { return CreateInternal(TValueInitializer.Empty); }
    }

    private static TSelf CreateInternal(TUnderlyingValue value)
    {
        var instance = (TSelf)RuntimeHelpers.GetUninitializedObject(typeof(TSelf));
        var setter = typeof(TSelf).GetProperty(nameof(Value), BindingFlags.Public | BindingFlags.Instance)!
            .GetSetMethod(nonPublic: true);

        setter!.Invoke(instance, [value]);
        return instance;
    }

    public static explicit operator TUnderlyingValue(BaseEntityId<TSelf, TUnderlyingValue, TValueInitializer> id)
    {
        return id.Value;
    }

    public static explicit operator string(BaseEntityId<TSelf, TUnderlyingValue, TValueInitializer> id)
    {
        return id.Value.ToString()!;
    }

    public virtual string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        return Value.ToString(format, formatProvider);
    }

    public virtual bool Equals(TUnderlyingValue other)
    {
        return Value.Equals(other);
    }

    public virtual bool Equals(TSelf? other)
    {
        return other != null && Value.Equals(other.Value);
    }

    public override string? ToString()
    {
        return Value.ToString();
    }

    public virtual int CompareTo(TSelf? other)
    {
        return other != null
            ? Value.CompareTo(other.Value)
            : 1;
    }

    public virtual int CompareTo(TUnderlyingValue? otherValue)
    {
        return otherValue != null
            ? Value.CompareTo(otherValue)
            : 1;
    }

    public virtual int CompareTo(BaseEntityId<TSelf, TUnderlyingValue, TValueInitializer>? other)
    {
        return other != null
            ? Value.CompareTo(other.Value)
            : 1;
    }

    public virtual int CompareTo(object? obj)
    {
        return Value.CompareTo(obj);
    }

    public virtual bool TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider = null)
    {
        return Value.TryFormat(
            utf8Destination,
            out bytesWritten,
            format,
            provider
        );
    }

    public virtual bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider = null)
    {
        return Value.TryFormat(destination, out charsWritten, format, provider);
    }

    public static TSelf Parse(
        ReadOnlySpan<char> s,
        IFormatProvider? provider = null)
    {
        return CreateInternal(TUnderlyingValue.Parse(s, provider));
    }

    public static bool TryParse(
        ReadOnlySpan<char> s,
        IFormatProvider? provider,
        [MaybeNullWhen(returnValue: false)] out TSelf result)
    {
        if (!TUnderlyingValue.TryParse(s, provider, out var value))
        {
            result = null;
            return false;
        }

        result = CreateInternal(value);
        return true;
    }

    public static TSelf Parse(string s, IFormatProvider? provider = null)
    {
        return CreateInternal(TUnderlyingValue.Parse(s, provider));
    }

    public static bool TryParse(
        [NotNullWhen(returnValue: true)] string? s,
        IFormatProvider? provider,
        [MaybeNullWhen(returnValue: false)] out TSelf result)
    {
        if (!TUnderlyingValue.TryParse(s, provider, out var value))
        {
            result = null;
            return false;
        }

        result = CreateInternal(value);
        return true;
    }
}