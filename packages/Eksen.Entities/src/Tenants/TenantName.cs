using Eksen.ValueObjects;

namespace Eksen.Entities.Tenants;

public sealed record TenantName : ValueObject<TenantName, string, TenantName>,
    IConcreteValueObject<TenantName, string>
{
    public const int MaxLength = 50;

    private TenantName(string value) : base(value) { }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw TenantErrors.EmptyTenantName.Raise();
        }

        value = value.Trim();

        if (value.Length > MaxLength)
        {
            throw TenantErrors.TenantNameOverflow.Raise(value, MaxLength);
        }

        return value;
    }

    public static TenantName Create(string value)
    {
        return Parse(value);
    }

    public static TenantName Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new TenantName(value);
    }
}