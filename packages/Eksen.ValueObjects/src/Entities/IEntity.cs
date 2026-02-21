namespace Eksen.ValueObjects.Entities;

public interface IEntity;

public interface IEntity<out TId, TValue> : IEntity
    where TId : IEntityId<TId, TValue>
    where TValue :
        IComparable,
        IComparable<TValue>,
        IEquatable<TValue>,
        ISpanFormattable,
        ISpanParsable<TValue>,
        IUtf8SpanFormattable
{
    TId Id { get; }
}