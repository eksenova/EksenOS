namespace Eksen.ValueObjects.Companies;

public sealed record TaxOffice : ValueObject<TaxOffice, string, TaxOffice>, IConcreteValueObject<TaxOffice, string>
{
    public const int MaxLength = 100;

    public static TaxOffice Create(string taxOffice)
    {
        return Parse(taxOffice);
    }

    public static TaxOffice Parse(string taxOffice, IFormatProvider? formatProvider = null)
    {
        return new TaxOffice(taxOffice);
    }

    private TaxOffice(string taxOffice) : base(taxOffice) { }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw CompanyErrors.EmptyTaxOffice.Raise();
        }

        value = value.Trim();

        if (value.Length > MaxLength)
        {
            throw CompanyErrors.TaxOfficeOverflow.Raise(value, MaxLength);
        }

        return value;
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }
}