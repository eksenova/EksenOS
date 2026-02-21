using Eksen.ValueObjects;

namespace Eksen.Entities.Roles;

public sealed record RoleName : ValueObject<RoleName, string, RoleName>,
    IConcreteValueObject<RoleName, string>
{
    public const int MaxLength = 50;

    private RoleName(string value) : base(value) { }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw RoleErrors.RoleNameEmpty.Raise();
        }

        value = value.Trim();

        if (value.Length > MaxLength)
        {
            throw RoleErrors.RoleNameOverflow.Raise(value, MaxLength);
        }

        return value;
    }

    public static RoleName Create(string value)
    {
        return Parse(value);
    }

    public static RoleName Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new RoleName(value);
    }
}