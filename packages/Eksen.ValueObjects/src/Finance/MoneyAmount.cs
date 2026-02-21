using System.Globalization;

namespace Eksen.ValueObjects.Finance;

public sealed record MoneyAmount
    : ValueObject<MoneyAmount, decimal, MoneyAmount>,
      IConcreteValueObject<MoneyAmount, decimal>
{
    public const decimal MaxValue = long.MaxValue;
    public const decimal MinValue = 0m;

    public const decimal MinPositiveValue = 0.01m;
    public const decimal Tolerance = 0.01m;

    private MoneyAmount(decimal value) : base(value)
    {
    }

    public static MoneyAmount Zero
    {
        get { return new MoneyAmount(value: 0m); }
    }

    protected override decimal Validate(decimal value)
    {
        if (value < 0)
        {
            throw FinanceErrors.NegativeMoneyAmount.Raise(value);
        }

        if (value > MaxValue)
        {
            throw FinanceErrors.MoneyAmountOverflow.Raise(value, MaxValue);
        }

        return value;
    }   

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value.ToString(format: "F4", provider ?? CultureInfo.InvariantCulture);
    }

    public static MoneyAmount Create(decimal value)
    {
        return new MoneyAmount(value);
    }

    public static MoneyAmount Parse(string value, IFormatProvider? formatProvider = null)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, formatProvider ?? CultureInfo.InvariantCulture, out var decimalValue))
        {
            throw FinanceErrors.InvalidMoneyAmount.Raise(value);
        }

        return new MoneyAmount(decimalValue);
    }

    public static implicit operator decimal(MoneyAmount moneyAmount)
    {
        return moneyAmount.Value;
    }

    public static explicit operator MoneyAmount(decimal value)
    {
        return new MoneyAmount(value);
    }

    public static MoneyAmount operator +(MoneyAmount a)
    {
        return a;
    }

    public static MoneyAmount operator +(MoneyAmount a, MoneyAmount b)
    {
        return new MoneyAmount(a.Value + b.Value);
    }

    public static MoneyAmount operator -(MoneyAmount a, MoneyAmount b)
    {
        return new MoneyAmount(a.Value - b.Value);
    }

    public static MoneyAmount operator *(MoneyAmount a, MoneyAmount b)
    {
        return new MoneyAmount(a.Value * b.Value);
    }

    public static MoneyAmount operator /(MoneyAmount a, MoneyAmount b)
    {
        return new MoneyAmount(a.Value / b.Value);
    }

    public static MoneyAmount operator +(MoneyAmount a, decimal b)
    {
        return new MoneyAmount(a.Value + b);
    }

    public static MoneyAmount operator -(MoneyAmount a, decimal b)
    {
        return new MoneyAmount(a.Value - b);
    }

    public static MoneyAmount operator *(MoneyAmount a, decimal b)
    {
        return new MoneyAmount(a.Value * b);
    }

    public static MoneyAmount operator /(MoneyAmount a, decimal b)
    {
        return new MoneyAmount(a.Value / b);
    }

    public static MoneyAmount operator *(MoneyAmount a, uint b)
    {
        return new MoneyAmount(a.Value * b);
    }

    public static MoneyAmount operator /(MoneyAmount a, uint b)
    {
        return new MoneyAmount(a.Value / b);
    }

    public void AssertPositive()
    {
        if (IsZero)
        {
            throw FinanceErrors.MoneyAmountNotPositive.Raise(Value);
        }
    }

    public bool IsZero
    {
        get { return Value < MinPositiveValue; }
    }

    public static decimal Round(decimal amount, int places = 2)
    {
        return Math.Round(amount, places, MidpointRounding.AwayFromZero);
    }
}