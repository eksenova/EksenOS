using Eksen.ValueObjects.Entities;

namespace Eksen.Repositories;

public record DefaultIdFindParameters<TId, TIdValue>
    where TId : IEntityId<TId, TIdValue>
    where TIdValue :
    IComparable,
    IComparable<TIdValue>,
    IEquatable<TIdValue>,
    ISpanFormattable,
    ISpanParsable<TIdValue>,
    IUtf8SpanFormattable
{
    public required TId Id { get; set; }
}