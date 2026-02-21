using System.ComponentModel;
using Eksen.ValueObjects.Entities;

namespace Eksen.Ulid;

public abstract record UlidEntityId<TSelf>
    : BaseEntityId<TSelf, System.Ulid, UlidValueInitializer>,
        IComparable<System.Ulid>,
        IEquatable<System.Ulid>
    where TSelf : UlidEntityId<TSelf>

{
    public const int Length = UlidConsts.Length;
    
    static UlidEntityId()
    {
        TypeDescriptor.AddAttributes(
            typeof(TSelf),
            new TypeConverterAttribute(typeof(EntityIdStringTypeConverter<TSelf, System.Ulid>)));
    }

    protected internal UlidEntityId(System.Ulid value) : base(value) { }

    public override int CompareTo(System.Ulid other)
    {
        return base.CompareTo(other);
    }
}