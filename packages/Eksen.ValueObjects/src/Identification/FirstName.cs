namespace Eksen.ValueObjects.Identification;

public sealed record FirstName : ValueObject<FirstName, string, FirstName>,
    IConcreteValueObject<FirstName, string>
{
    public const int MaxLength = 24;

    private FirstName(string value) : base(value) { }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw IdentificationErrors.EmptyFirstName.Raise();
        }

        value = value.Trim();

        if (value.Length > MaxLength)
        {
            throw IdentificationErrors.FirstNameOverflow.Raise(value, MaxLength);
        }

        return value;
    }

    public static FirstName Create(string value)
    {
        return Parse(value);
    }

    public static FirstName Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new FirstName(value);
    }
}