using System.ComponentModel;

namespace Eksen.ValueObjects.Entities;

public static class GuidConsts
{
    public const int Length = 36;
}

public abstract class GuidValueInitializer : IEntityIdValueInitializer<Guid>
{
    private GuidValueInitializer() { }

    public static Guid Empty
    {
        get
        {
            return Guid.Empty;
        }
    }

    public static Guid New()
    {
        return Guid.NewGuid();
    }
}

public abstract record GuidEntityId<TSelf> 
    : BaseEntityId<TSelf, Guid, GuidValueInitializer>,
        IComparable<Guid>,
        IEquatable<Guid> 
    where TSelf : GuidEntityId<TSelf>
   
{
    public const int Length = GuidConsts.Length;
    
    static GuidEntityId()
    {
        TypeDescriptor.AddAttributes(
            typeof(TSelf),
            new TypeConverterAttribute(typeof(EntityIdStringTypeConverter<TSelf, Guid>)));
    }

    protected internal GuidEntityId(Guid value) : base(value) { }

    public override int CompareTo(Guid other)
    {
        return base.CompareTo(other);
    }
}