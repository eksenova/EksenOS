namespace Eksen.ValueObjects.Entities;

public interface IEntityIdValueInitializer<out TValue>
{
    static abstract TValue Empty { get; }

    static abstract TValue New();
}