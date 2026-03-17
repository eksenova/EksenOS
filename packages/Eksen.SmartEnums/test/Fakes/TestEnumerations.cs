using Eksen.SmartEnums;

namespace Eksen.SmartEnums.Tests.Fakes;

public sealed record TestColor : Enumeration<TestColor>
{
    public static readonly TestColor Red = new(nameof(Red));
    public static readonly TestColor Green = new(nameof(Green));
    public static readonly TestColor Blue = new(nameof(Blue));

    private TestColor(string code) : base(code) { }
}

public sealed record TestSize : Enumeration<TestSize>
{
    public static readonly TestSize Small = new(nameof(Small));
    public static readonly TestSize Medium = new(nameof(Medium));
    public static readonly TestSize Large = new(nameof(Large));
    public static readonly TestSize ExtraLarge = new(nameof(ExtraLarge));

    private TestSize(string code) : base(code) { }
}
