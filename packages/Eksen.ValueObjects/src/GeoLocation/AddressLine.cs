namespace Eksen.ValueObjects.GeoLocation;

public sealed record AddressLine : ValueObject<AddressLine, string, AddressLine>,
    IConcreteValueObject<AddressLine, string>
{
    public const int MaxLength = 255;

    private AddressLine(string value) : base(value) { }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw GeoLocationErrors.EmptyAddressLine.Raise();
        }

        var trimmedValue = value.Trim();

        return trimmedValue.Length switch
        {
            > MaxLength => throw GeoLocationErrors.AddressLineOverflow.Raise(value, MaxLength),
            _ => trimmedValue
        };
    }

    public static AddressLine Create(string value)
    {
        return Parse(value);
    }

    public static AddressLine Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new AddressLine(value);
    }
}