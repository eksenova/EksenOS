namespace Eksen.ValueObjects.Hashing;

public sealed record PasswordHash : ValueObject<PasswordHash, string, PasswordHash>,
    IConcreteValueObject<PasswordHash, string>
{
    public const int MaxLength = 256;

    private PasswordHash(string value) : base(value) { }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw HashingErrors.EmptyHash.Raise();
        }

        if (value.Length > MaxLength)
        {
            HashingErrors.HashOverflow.Raise(value, MaxLength);
        }

        return value;
    }

    public static PasswordHash Create(string value)
    {
        return Parse(value);
    }

    public static PasswordHash Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new PasswordHash(value);
    }
}