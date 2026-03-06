using System.Text.RegularExpressions;

namespace Eksen.ValueObjects.Telecommunications;

public sealed partial record GsmPhoneNumber : ValueObject<GsmPhoneNumber, string>,
    IValueObjectParser<GsmPhoneNumber, string>
{
    public const int MaxLength = 20;

    [GeneratedRegex(pattern: @"^\+?[0-9\s\-\(\)]{10,20}$")]
    private static partial Regex GsmPhoneNumberRegex();

    private GsmPhoneNumber(string value) : base(value) { }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw TelecommunicationErrors.EmptyGsmPhoneNumber.Raise();
        }

        value = value.Trim();

        if (value.Length > MaxLength)
        {
            throw TelecommunicationErrors.GsmPhoneNumberOverflow.Raise(value, MaxLength);
        }

        if (!GsmPhoneNumberRegex().IsMatch(value))
        {
            throw TelecommunicationErrors.InvalidGsmPhoneNumber.Raise(value);
        }

        return value;
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    public static GsmPhoneNumber Create(string value)
    {
        return Parse(value);
    }

    public static GsmPhoneNumber Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new GsmPhoneNumber(value);
    }
}
