using Eksen.ErrorHandling;
using Eksen.ValueObjects;

namespace Eksen.TestBase;

public record TestEntityName : ValueObject<TestEntityName, string>,
    IValueObjectParser<TestEntityName, string>
{
    public TestEntityName(string value) : base(value) { }

    private static readonly ErrorDescriptor EmptyError = new(ErrorType.Validation, codeCategory: "EksenTest", memberName: "Empty");
    private static readonly ErrorDescriptor OverflowError = new(ErrorType.Validation, codeCategory: "EksenTest", memberName: "Overflow");

    public const int MaxLength = 200;

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw EmptyError.Raise();
        }

        value = value.Trim();

        if (value.Length > MaxLength)
        {
            throw OverflowError.Raise();
        }

        return value;
    }

    public static TestEntityName Create(string value)
    {
        return Parse(value);
    }

    public static TestEntityName Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new TestEntityName(value);
    }
}