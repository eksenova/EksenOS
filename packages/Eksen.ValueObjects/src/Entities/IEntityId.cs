namespace Eksen.ValueObjects.Entities;

public interface IEntityId<TSelf, out TUnderlyingValue> :
    IComparable,
    IComparable<TSelf>,
    IEquatable<TSelf>,
    ISpanFormattable,
    ISpanParsable<TSelf>,
    IUtf8SpanFormattable, IValueObject<TSelf, TUnderlyingValue>
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
    static abstract TSelf NewId();
}