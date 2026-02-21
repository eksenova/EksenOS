using Eksen.ValueObjects.Entities;

namespace Eksen.Ulid;

public abstract class UlidValueInitializer : IEntityIdValueInitializer<System.Ulid>
{
    private UlidValueInitializer() { }

    public static System.Ulid Empty
    {
        get { return System.Ulid.Empty; }
    }

    public static System.Ulid New()
    {
        return System.Ulid.NewUlid();
    }
}