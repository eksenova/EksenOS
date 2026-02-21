namespace Eksen.Entities.Tenants;

public sealed record TenantName
{
    public const int MaxLength = 50;

    public string Value { get; private set; }

    public TenantName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw TenantErrors.EmptyTenantName.Raise();
        }

        name = name.Trim();

        if (name.Length > MaxLength)
        {
            throw TenantErrors.TenantNameOverflow.Raise(name, MaxLength);
        }

        Value = name;
    }
}