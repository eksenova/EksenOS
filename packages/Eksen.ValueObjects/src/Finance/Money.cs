namespace Eksen.ValueObjects.Finance;

public sealed record Money : ValueObject<Money, (Currency Currency, MoneyAmount Amount)>,
    IValueObjectParser<Money, (Currency Currency, MoneyAmount Amount)>
{
    private Money((Currency, MoneyAmount) value) : base(value) { }

    private Money(Currency currency, MoneyAmount amount) : this((currency, amount)) { }

    public Currency Currency
    {
        get { return Value.Currency; }
    }

    public MoneyAmount Amount
    {
        get { return Value.Amount; }
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return string.Format(provider, format: "{0:F2} {1}", Value.Amount.Value, Currency.Code);
    }

    protected override (Currency, MoneyAmount) Validate((Currency, MoneyAmount) value)
    {
        return value;
    }

    public static Money Create((Currency, MoneyAmount) value)
    {
        return new Money(value);
    }

    public static Money Parse(string value, IFormatProvider? formatProvider = null)
    {
        var parts = value.Split(
            separator: ' ',
            StringSplitOptions.RemoveEmptyEntries
            | StringSplitOptions.TrimEntries);

        if (parts.Length != 2)
        {
            throw FinanceErrors.InvalidMoneyFormat.Raise(value);
        }

        var amount = MoneyAmount.Parse(parts[0], formatProvider);
        var currency = Currency.Parse(parts[1]);

        return new Money((currency, amount));
    }
}
