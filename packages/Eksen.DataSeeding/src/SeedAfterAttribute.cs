namespace Eksen.DataSeeding;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SeedAfterAttribute(Type type) : Attribute
{
    public Type Type { get; } = type;
}