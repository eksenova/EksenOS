using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Eksen.SmartEnums.Tests.Fakes;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.SmartEnums.Tests;

public class JsonSmartEnumTypeInfoResolverTests : EksenUnitTestBase
{
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        var smartEnumOptions = new EksenSmartEnumOptions();
        smartEnumOptions.Add<TestColor>();
        smartEnumOptions.ConfigureJsonOptions(options);

        return options;
    }

    [Fact]
    public void Ignored_Enumeration_Property_Should_Stay_Out_Of_The_Contract()
    {
        // Arrange
        var options = CreateOptions();

        // Act
        var typeInfo = options.GetTypeInfo(typeof(RedPaint));

        // Assert
        typeInfo.Properties
            .Where(x => x.Get != null || x.Set != null)
            .Select(x => x.Name)
            .ShouldNotContain("color");
    }

    [Fact]
    public void Ignored_Enumeration_Property_Should_Not_Clash_With_The_Type_Discriminator()
    {
        // Arrange
        var options = CreateOptions();

        // Act
        var schema = JsonSchemaExporter.GetJsonSchemaAsNode(options, typeof(BasePaint));

        // Assert
        schema.ShouldNotBeNull();
    }

    [Fact]
    public void Writable_Enumeration_Property_Should_Still_Be_Rendered_As_String()
    {
        // Arrange
        var options = CreateOptions();

        // Act
        var json = JsonSerializer.Serialize(new ColoredBox { Color = TestColor.Green }, options);

        // Assert
        json.ShouldBe("""{"color":"Green"}""");
    }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "color")]
[JsonDerivedType(typeof(RedPaint), typeDiscriminator: "Red")]
public abstract record BasePaint
{
    [JsonIgnore]
    public abstract TestColor Color { get; }

    public required string Name { get; set; }
}

public sealed record RedPaint : BasePaint
{
    [JsonIgnore]
    public override TestColor Color
    {
        get
        {
            return TestColor.Red;
        }
    }
}

public sealed record ColoredBox
{
    public required TestColor Color { get; set; }
}
