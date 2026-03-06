using System.Text.RegularExpressions;

namespace Eksen.ValueObjects.Telecommunications;

public sealed partial record PhoneNumber : ValueObject<PhoneNumber, string>,
    IValueObjectParser<PhoneNumber, string>
{
    public const int MaxLength = 20;

    [GeneratedRegex(pattern: @"^\+?[0-9\s\-\(\)]{7,20}$")]
    private static partial Regex PhoneNumberRegex();

    private PhoneNumber(string value) : base(value) { }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw TelecommunicationErrors.EmptyPhoneNumber.Raise();
        }

        value = value.Trim();

        if (value.Length > MaxLength)
        {
            throw TelecommunicationErrors.PhoneNumberOverflow.Raise(value, MaxLength);
        }

        if (!PhoneNumberRegex().IsMatch(value))
        {
            throw TelecommunicationErrors.InvalidPhoneNumber.Raise(value);
        }

        return value;
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    public static PhoneNumber Create(string value)
    {
        return Parse(value);
    }

    public static PhoneNumber Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new PhoneNumber(value);
    }
}
