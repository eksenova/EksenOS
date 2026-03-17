using Eksen.SmartEnums;

namespace Eksen.SmartEnums.OpenApi.Tests.Fakes;

public sealed record TestColor : Enumeration<TestColor>
{
    public static readonly TestColor Red = new(nameof(Red));
    public static readonly TestColor Green = new(nameof(Green));
    public static readonly TestColor Blue = new(nameof(Blue));

    private TestColor(string code) : base(code) { }
}

public class ModelWithEnum
{
    public TestColor? Color { get; set; }
    public string? Name { get; set; }
}

public class ModelWithEnumList
{
    public List<TestColor>? Colors { get; set; }
}
