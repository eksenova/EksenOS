namespace Eksen.ValueObjects.Identification;

public sealed record FullName : ValueObject<FullName, string, FullName>,
    IConcreteValueObject<FullName, string>
{
    public const int MaxLength = 100;

    private FullName(string value) : base(value)
    {
        
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw IdentificationErrors.EmptyFullName.Raise();
        }

        value = value.Trim();

        if (value.Length > MaxLength)
        {
            throw IdentificationErrors.FullNameOverflow.Raise(value, MaxLength);
        }

        return value;
    }


    public static FullName Create(string value)
    {
        return Parse(value);
    }

    public static FullName Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new FullName(value);
    }
}