using System.Globalization;
using System.Runtime.CompilerServices;
using Eksen.SmartEnums;
using JetBrains.Annotations;

namespace Eksen.ValueObjects.Measurements;

public sealed record WeightUnit : Enumeration<WeightUnit>
{
    public static readonly WeightUnit Kilogram = new(displayName: "kg", conversionToKg: 1m);
    public static readonly WeightUnit Gram = new(displayName: "g", conversionToKg: 0.001m);
    public static readonly WeightUnit Ton = new(displayName: "t", conversionToKg: 1000m);
    public static readonly WeightUnit Pound = new(displayName: "lb", conversionToKg: 0.45359237m);

    public string DisplayName { get; }

    public decimal ConversionToKg { get; }

    private WeightUnit(
        string displayName,
        decimal conversionToKg,
        [CallerMemberName] string? code = null) : base(code)
    {
        DisplayName = displayName;
        ConversionToKg = conversionToKg;
    }
}

public sealed record Weight : ValueObject<Weight, (decimal Amount, WeightUnit Unit)>,
    IValueObjectParser<Weight, (decimal Amount, WeightUnit Unit)>
{
    public const decimal MinValue = 0m;
    public const decimal MaxValue = 999_999_999m;

    private Weight((decimal Amount, WeightUnit Unit) value) : base(value)
    {
    }

    [UsedImplicitly]
    private Weight(decimal Amount, WeightUnit Unit) : this((Amount, Unit)) { }

    public decimal Amount
    {
        get { return Value.Amount; }
    }

    public WeightUnit Unit
    {
        get { return Value.Unit; }
    }

    public Weight ConvertTo(WeightUnit targetUnit)
    {
        var amountInKg = Amount * Unit.ConversionToKg;
        var convertedAmount = amountInKg / targetUnit.ConversionToKg;

        return new Weight((convertedAmount, targetUnit));
    }

    public decimal ToKilograms()
    {
        return Amount * Unit.ConversionToKg;
    }

    public decimal ToGrams()
    {
        return ToKilograms() * 1000m;
    }

    public decimal ToTons()
    {
        return ToKilograms() / 1000m;
    }

    protected override (decimal Amount, WeightUnit Unit) Validate((decimal Amount, WeightUnit Unit) value)
    {
        if (value.Amount < MinValue)
        {
            throw MeasurementErrors.NegativeWeight.Raise(value.Amount);
        }

        if (value.Amount > MaxValue)
        {
            throw MeasurementErrors.WeightTooLarge.Raise(value.Amount, MaxValue);
        }

        return value;
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return string.Format(provider ?? CultureInfo.InvariantCulture, format: "{0:F4} {1}", Value.Amount, Value.Unit.Code);
    }

    public static Weight Create((decimal Amount, WeightUnit Unit) value)
    {
        return new Weight(value);
    }

    public static Weight Parse(string value, IFormatProvider? formatProvider = null)
    {
        var parts = value.Split(
            separator: ' ',
            StringSplitOptions.RemoveEmptyEntries
            | StringSplitOptions.TrimEntries);

        if (parts.Length != 2)
        {
            throw MeasurementErrors.InvalidWeightFormat.Raise(value);
        }

        if (!decimal.TryParse(parts[0], NumberStyles.Number, formatProvider ?? CultureInfo.InvariantCulture, out var amount))
        {
            throw MeasurementErrors.InvalidWeightFormat.Raise(value);
        }

        var unit = WeightUnit.Parse(parts[1]);

        return new Weight((amount, unit));
    }
}
