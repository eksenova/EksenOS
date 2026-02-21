namespace Eksen.ValueObjects.Identification;

public sealed record LastName : ValueObject<LastName, string, LastName>,
    IConcreteValueObject<LastName, string>
{
    public const int MaxLength = 24;

    private LastName(string value) : base(value) { }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw IdentificationErrors.EmptyLastName.Raise();
        }

        value = value.Trim();

        if (value.Length > MaxLength)
        {
            throw IdentificationErrors.LastNameOverflow.Raise(value, MaxLength);
        }

        return value;
    }

    public static LastName Create(string value)
    {
        return Parse(value);
    }

    public static LastName Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new LastName(value);
    }
}