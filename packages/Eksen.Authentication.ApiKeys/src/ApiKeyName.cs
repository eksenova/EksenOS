using Eksen.ValueObjects;

namespace Eksen.Authentication.ApiKeys;

public sealed record ApiKeyName : ValueObject<ApiKeyName, string>, IValueObjectParser<ApiKeyName, string>
{
    public const int MaxLength = 100;

    private ApiKeyName(string value) : base(value)
    {
    }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw ApiKeyErrors.EmptyApiKeyName.Raise();
        }

        value = value.Trim();

        if (value.Length > MaxLength)
        {
            throw ApiKeyErrors.ApiKeyNameOverflow.Raise(value, MaxLength);
        }

        return value;
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    public static ApiKeyName Create(string value)
    {
        return new ApiKeyName(value);
    }

    public static ApiKeyName Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new ApiKeyName(value);
    }
}
