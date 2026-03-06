namespace Eksen.ValueObjects.Companies;

public sealed record CompanyTitle : ValueObject<CompanyTitle, string>, IValueObjectParser<CompanyTitle, string>
{
    public const int MaxLength = 200;

    private CompanyTitle(string value) : base(value) { }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw CompanyErrors.EmptyCompanyTitle.Raise();
        }

        value = value.Trim();

        if (value.Length > MaxLength)
        {
            throw CompanyErrors.CompanyTitleOverflow.Raise(value, MaxLength);
        }

        return value;
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    public static CompanyTitle Create(string value)
    {
        return Parse(value);
    }

    public static CompanyTitle Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new CompanyTitle(value);
    }
}
