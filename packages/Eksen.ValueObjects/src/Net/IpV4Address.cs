using System.Text.RegularExpressions;

namespace Eksen.ValueObjects.Net;

public sealed partial record IpV4Address : ValueObject<IpV4Address, string, IpV4Address>,
    IConcreteValueObject<IpV4Address, string>
{
    public const int MaxLength = 15;

    private IpV4Address(string value) : base(value) { }

    [GeneratedRegex(pattern: @"^((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}$")]
    private static partial Regex GetRegex();

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw NetErrors.EmptyIpAddress.Raise();
        }

        value = value.Trim();

        if (value.Length > MaxLength)
        {
            throw NetErrors.IpV4AddressOverflow.Raise(value, MaxLength);
        }

        if (!GetRegex().IsMatch(value))
        {
            throw NetErrors.InvalidIpV4Address.Raise(value);
        }

        return value;
    }

    public static IpV4Address Create(string value)
    {
        return Parse(value);
    }

    public static IpV4Address Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new IpV4Address(value);
    }
}