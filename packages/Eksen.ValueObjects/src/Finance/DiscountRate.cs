using System.Globalization;

namespace Eksen.ValueObjects.Finance;

public sealed record DiscountRate : ValueObject<DiscountRate, decimal>,
    IValueObjectParser<DiscountRate, decimal>
{
    public const decimal MinValue = 0m;
    public const decimal MaxValue = 100m;

    private DiscountRate(decimal value) : base(value)
    {
    }

    public static DiscountRate Zero
    {
        get { return new DiscountRate(value: 0m); }
    }

    protected override decimal Validate(decimal value)
    {
        if (value < MinValue)
        {
            throw FinanceErrors.NegativeDiscountRate.Raise(value);
        }

        if (value > MaxValue)
        {
            throw FinanceErrors.DiscountRateOverflow.Raise(value, MaxValue);
        }

        return value;
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value.ToString(format: "F2", provider ?? CultureInfo.InvariantCulture);
    }

    public static DiscountRate Create(decimal value)
    {
        return new DiscountRate(value);
    }

    public static DiscountRate Parse(string value, IFormatProvider? formatProvider = null)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, formatProvider ?? CultureInfo.InvariantCulture, out var decimalValue))
        {
            throw FinanceErrors.InvalidDiscountRate.Raise(value);
        }

        return new DiscountRate(decimalValue);
    }

    public static implicit operator decimal(DiscountRate discountRate)
    {
        return discountRate.Value;
    }

    public static explicit operator DiscountRate(decimal value)
    {
        return new DiscountRate(value);
    }
}
