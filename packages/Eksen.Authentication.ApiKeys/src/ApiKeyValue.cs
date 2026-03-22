using Eksen.ValueObjects;

namespace Eksen.Authentication.ApiKeys;

public sealed record ApiKeyValue : ValueObject<ApiKeyValue, string>, IValueObjectParser<ApiKeyValue, string>
{
    public const int MaxLength = 128;

    private ApiKeyValue(string value) : base(value)
    {
    }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw ApiKeyErrors.EmptyApiKeyValue.Raise();
        }

        value = value.Trim();

        if (value.Length > MaxLength)
        {
            throw ApiKeyErrors.ApiKeyValueOverflow.Raise(value, MaxLength);
        }

        return value;
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    public static ApiKeyValue Create(string value)
    {
        return new ApiKeyValue(value);
    }

    public static ApiKeyValue Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new ApiKeyValue(value);
    }
}
