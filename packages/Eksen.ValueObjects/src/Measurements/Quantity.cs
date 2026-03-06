using System.Globalization;
using System.Runtime.CompilerServices;
using Eksen.SmartEnums;
using JetBrains.Annotations;

namespace Eksen.ValueObjects.Measurements;

public sealed record QuantityUnit : Enumeration<QuantityUnit>
{
    public static readonly QuantityUnit Piece = new(displayName: "Adet");
    public static readonly QuantityUnit Box = new(displayName: "Kutu");
    public static readonly QuantityUnit Pallet = new(displayName: "Palet");
    public static readonly QuantityUnit Set = new(displayName: "Takım");

    public string DisplayName { get; }

    private QuantityUnit(string displayName, [CallerMemberName] string? code = null) : base(code)
    {
        DisplayName = displayName;
    }
}

public sealed record Quantity : ValueObject<Quantity, (decimal Amount, QuantityUnit Unit)>,
    IValueObjectParser<Quantity, (decimal Amount, QuantityUnit Unit)>
{
    public const decimal MinValue = 0.0001m;
    public const decimal MaxValue = 999_999_999m;

    private Quantity((decimal Amount, QuantityUnit Unit) value) : base(value)
    {
    }

    [UsedImplicitly]
    private Quantity(decimal Amount, QuantityUnit Unit) : this((Amount, Unit)) { }

    public decimal Amount
    {
        get { return Value.Amount; }
    }

    public QuantityUnit Unit
    {
        get { return Value.Unit; }
    }

    protected override (decimal Amount, QuantityUnit Unit) Validate((decimal Amount, QuantityUnit Unit) value)
    {
        if (value.Amount < MinValue)
        {
            throw MeasurementErrors.QuantityTooSmall.Raise(value.Amount);
        }

        if (value.Amount > MaxValue)
        {
            throw MeasurementErrors.QuantityTooLarge.Raise(value.Amount, MaxValue);
        }

        return value;
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return string.Format(provider ?? CultureInfo.InvariantCulture, format: "{0:F4} {1}", Value.Amount, Value.Unit.Code);
    }

    public static Quantity Create((decimal Amount, QuantityUnit Unit) value)
    {
        return new Quantity(value);
    }

    public static Quantity Parse(string value, IFormatProvider? formatProvider = null)
    {
        var parts = value.Split(
            separator: ' ',
            StringSplitOptions.RemoveEmptyEntries
            | StringSplitOptions.TrimEntries);

        if (parts.Length != 2)
        {
            throw MeasurementErrors.InvalidQuantityFormat.Raise(value);
        }

        if (!decimal.TryParse(parts[0], NumberStyles.Number, formatProvider ?? CultureInfo.InvariantCulture, out var amount))
        {
            throw MeasurementErrors.InvalidQuantityFormat.Raise(value);
        }

        var unit = QuantityUnit.Parse(parts[1]);

        return new Quantity((amount, unit));
    }
}
