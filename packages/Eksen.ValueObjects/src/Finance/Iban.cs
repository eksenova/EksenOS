using System.Text.RegularExpressions;

namespace Eksen.ValueObjects.Finance;

public sealed partial record Iban : ValueObject<Iban, string, Iban>,
    IConcreteValueObject<Iban, string>
{
    public const int MaxLength = 34;

    [GeneratedRegex(pattern: @"^[A-Z]{2}[0-9]{2}[A-Z0-9]{1,30}$")]
    private static partial Regex IbanRegex();

    private Iban(string value) : base(value)
    {
    }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw FinanceErrors.EmptyIban.Raise();
        }

        value = value.Trim().Replace(oldValue: " ", newValue: "").ToUpperInvariant();

        if (value.Length > MaxLength)
        {
            throw FinanceErrors.IbanOverflow.Raise(value, MaxLength);
        }

        if (!IbanRegex().IsMatch(value))
        {
            throw FinanceErrors.InvalidIban.Raise(value);
        }

        return value;
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    public static Iban Create(string value)
    {
        return Parse(value);
    }

    public static Iban Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new Iban(value);
    }
}
