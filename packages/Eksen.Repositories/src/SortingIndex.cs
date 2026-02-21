using System.Globalization;
using Eksen.ValueObjects;

namespace Eksen.Repositories;

public sealed record SortingIndex
    : ValueObject<SortingIndex, int, SortingIndex>,
        IConcreteValueObject<SortingIndex, int>,
        IComparable<SortingIndex>
{
    private SortingIndex(int value) : base(value) {
    }

    protected override int Validate(int value)
    {
        if (value < 0)
        {
            throw RepositoriesErrors.NegativeSortingIndex.Raise(value);
        }

        return value;
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value.ToString(provider ?? CultureInfo.InvariantCulture);
    }

    public static SortingIndex Create(int value)
    {
        return new SortingIndex(value);
    }

    public static SortingIndex Parse(string value, IFormatProvider? formatProvider = null)
    {
        if (!int.TryParse(value, NumberStyles.Integer, formatProvider ?? CultureInfo.InvariantCulture, out var intValue))
        {
            throw RepositoriesErrors.InvalidSortingIndex.Raise(value);
        }

        return new SortingIndex(intValue);
    }

    public int CompareTo(SortingIndex? other)
    {
        return Value.CompareTo(other?.Value);
    }
}