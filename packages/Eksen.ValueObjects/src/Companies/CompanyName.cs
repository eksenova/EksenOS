namespace Eksen.ValueObjects.Companies;

public sealed record CompanyName : ValueObject<CompanyName, string, CompanyName>, IConcreteValueObject<CompanyName, string>
{
    public const int MaxLength = 50;

    public static CompanyName Create(string companyName)
    {
        return Parse(companyName);
    }

    public static CompanyName Parse(string companyName, IFormatProvider? formatProvider = null)
    {
        return new CompanyName(companyName);
    }

    private CompanyName(string companyName) : base(companyName) { }

    protected override string Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw CompanyErrors.EmptyCompanyName.Raise();
        }

        name = name.Trim();

        if (name.Length > MaxLength)
        {
            throw CompanyErrors.CompanyNameOverflow.Raise(name, MaxLength);
        }

        return name;
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }
}