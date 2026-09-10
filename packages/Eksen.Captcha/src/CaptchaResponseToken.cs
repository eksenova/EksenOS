using Eksen.ValueObjects;

namespace Eksen.Captcha;

public sealed record CaptchaResponseToken : ValueObject<CaptchaResponseToken, string>,
    IValueObjectParser<CaptchaResponseToken, string>
{
    public const int MaxLength = 4096;

    private CaptchaResponseToken(string value) : base(value) { }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw CaptchaErrors.EmptyResponseToken.Raise();
        }

        if (value.Length > MaxLength)
        {
            throw CaptchaErrors.ResponseTokenOverflow.Raise(value, MaxLength);
        }

        return value;
    }

    public static CaptchaResponseToken Create(string value)
    {
        return Parse(value);
    }

    public static CaptchaResponseToken Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new CaptchaResponseToken(value);
    }
}
