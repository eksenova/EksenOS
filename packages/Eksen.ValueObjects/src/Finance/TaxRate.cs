using System.Globalization;

namespace Eksen.ValueObjects.Finance;

public sealed record TaxRate : ValueObject<TaxRate, decimal>,
    IValueObjectParser<TaxRate, decimal>
{
    public const decimal MinValue = 0m;
    public const decimal MaxValue = 100m;

    private TaxRate(decimal value) : base(value)
    {
    }

    public static TaxRate Zero
    {
        get { return new TaxRate(value: 0m); }
    }

    protected override decimal Validate(decimal value)
    {
        if (value < MinValue)
        {
            throw FinanceErrors.NegativeTaxRate.Raise(value);
        }

        if (value > MaxValue)
        {
            throw FinanceErrors.TaxRateOverflow.Raise(value, MaxValue);
        }

        return value;
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value.ToString(format: "F2", provider ?? CultureInfo.InvariantCulture);
    }

    public static TaxRate Create(decimal value)
    {
        return new TaxRate(value);
    }

    public static TaxRate Parse(string value, IFormatProvider? formatProvider = null)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, formatProvider ?? CultureInfo.InvariantCulture, out var decimalValue))
        {
            throw FinanceErrors.InvalidTaxRate.Raise(value);
        }

        return new TaxRate(decimalValue);
    }

    public static implicit operator decimal(TaxRate taxRate)
    {
        return taxRate.Value;
    }

    public static explicit operator TaxRate(decimal value)
    {
        return new TaxRate(value);
    }
}
