using Eksen.ValueObjects;

namespace Eksen.Permissions;

public sealed record PermissionName : ValueObject<PermissionName, string, PermissionName>,
    IConcreteValueObject<PermissionName, string>
{
    public const int MaxLength = 50;

    private PermissionName(string value) : base(value) { }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw PermissionErrors.EmptyPermissionName.Raise();
        }

        value = value.Trim();

        if (value.Length > MaxLength)
        {
            throw PermissionErrors.PermissionNameOverflow.Raise(value, MaxLength);
        }

        return value;
    }

    public static PermissionName Create(string value)
    {
        return Parse(value);
    }

    public static PermissionName Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new PermissionName(value);
    }

    public static implicit operator PermissionName(string value)
    {
        return new PermissionName(value);
    }
}