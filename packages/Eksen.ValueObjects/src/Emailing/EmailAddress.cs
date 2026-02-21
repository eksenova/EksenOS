using System.Text.RegularExpressions;

namespace Eksen.ValueObjects.Emailing;

public sealed partial record EmailAddress : ValueObject<EmailAddress, string, EmailAddress>,
    IConcreteValueObject<EmailAddress, string>
{
    public const int MaxLength = 50;

    [GeneratedRegex(pattern: "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$")]
    private static partial Regex EmailAddressRegex();

    private EmailAddress(string email) : base(email)
    {
        
    }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw EmailingErrors.EmptyEmailAddress.Raise();
        }

        value = value.Trim();

        if (value.Length > MaxLength)
        {
            throw EmailingErrors.EmailAddressOverflow.Raise(value, MaxLength);
        }

        if (!EmailAddressRegex().IsMatch(value))
        {
            throw EmailingErrors.InvalidEmailAddress.Raise(value);
        }

        return value.ToLowerInvariant();
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    public static EmailAddress Create(string value)
    {
        return Parse(value);
    }

    public static EmailAddress Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new EmailAddress(value);
    }
}