namespace Eksen.ValueObjects.Entities;

public interface IEntityId<TSelf, TUnderlyingValue> :
    IComparable,
    IComparable<TSelf>,
    IEquatable<TSelf>,
    ISpanFormattable,
    ISpanParsable<TSelf>,
    IUtf8SpanFormattable
    where TSelf :
    IEntityId<TSelf, TUnderlyingValue>
    where TUnderlyingValue :
    IComparable,
    IComparable<TUnderlyingValue>,
    IEquatable<TUnderlyingValue>,
    ISpanFormattable,
    ISpanParsable<TUnderlyingValue>,
    IUtf8SpanFormattable
{
    TUnderlyingValue Value { get; init; }

    static abstract TSelf NewId();
}