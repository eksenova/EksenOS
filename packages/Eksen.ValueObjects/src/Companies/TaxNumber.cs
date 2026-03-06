namespace Eksen.ValueObjects.Companies;

public sealed record TaxNumber : ValueObject<TaxNumber, string>, IValueObjectParser<TaxNumber, string>
{
    public const int MaxLength = 20;

    private TaxNumber(string value) : base(value) { }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw CompanyErrors.EmptyTaxNumber.Raise();
        }

        value = value.Trim();

        if (value.Length > MaxLength)
        {
            throw CompanyErrors.TaxNumberOverflow.Raise(value, MaxLength);
        }

        return value;
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    public static TaxNumber Create(string value)
    {
        return Parse(value);
    }

    public static TaxNumber Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new TaxNumber(value);
    }
}
