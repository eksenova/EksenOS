namespace Eksen.ValueObjects.Http;

public sealed record UserAgent : ValueObject<UserAgent, string, UserAgent>,
    IConcreteValueObject<UserAgent, string>
{
    public const int MaxLength = 255;

    private UserAgent(string value) : base(value) { }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw HttpErrors.EmptyUserAgent.Raise();
        }

        if (value.Length > MaxLength)
        {
            throw HttpErrors.UserAgentOverflow.Raise(value, MaxLength);
        }

        return value;
    }

    public static UserAgent Create(string value)
    {
        return Parse(value);
    }

    public static UserAgent Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new UserAgent(value);
    }
}