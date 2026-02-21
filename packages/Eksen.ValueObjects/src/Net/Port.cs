namespace Eksen.ValueObjects.Net;

public sealed record Port : ValueObject<Port, int, Port>,
    IConcreteValueObject<Port, int>
{
    public const int MinValue = ushort.MinValue;
    public const int MaxValue = ushort.MaxValue;

    private Port(int value) : base(value) { }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value.ToString(provider);
    }

    protected override int Validate(int value)
    {
        if (value is < MinValue or > MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"Port numarası {MinValue} ile {MaxValue} arasında olmalıdır.");
        }

        return value;
    }

    public static Port Create(int value)
    {
        return new Port(value);
    }

    public static Port Parse(string value, IFormatProvider? formatProvider = null)
    {
        if (!int.TryParse(value, out var result))
        {
            throw NetErrors.InvalidPort.Raise(value);
        }

        return new Port(result);
    }
}