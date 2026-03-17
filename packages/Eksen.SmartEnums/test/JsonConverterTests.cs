using System.Text.Json;
using Eksen.SmartEnums;
using Eksen.SmartEnums.Tests.Fakes;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.SmartEnums.Tests;

public class JsonStringSmartEnumConverterTests : EksenUnitTestBase
{
    private readonly JsonSerializerOptions _options;

    public JsonStringSmartEnumConverterTests()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new JsonStringSmartEnumConverter<TestColor>());
    }

    [Fact]
    public void Serialize_Should_Write_Code_As_String()
    {
        // Arrange & Act
        var json = JsonSerializer.Serialize(TestColor.Red, _options);

        // Assert
        json.ShouldBe("\"Red\"");
    }

    [Fact]
    public void Deserialize_Should_Parse_Code_String()
    {
        // Arrange & Act
        var result = JsonSerializer.Deserialize<TestColor>("\"Green\"", _options);

        // Assert
        result.ShouldBe(TestColor.Green);
    }

    [Fact]
    public void Deserialize_Should_Throw_When_Invalid()
    {
        // Arrange & Act & Assert
        Should.Throw<Exception>(() =>
            JsonSerializer.Deserialize<TestColor>("\"Yellow\"", _options));
    }
}

public class JsonStringEnumerationConverterFactoryTests : EksenUnitTestBase
{
    [Fact]
    public void CanConvert_Should_Return_True_For_Enumeration_Type()
    {
        // Arrange
        var factory = new JsonStringEnumerationConverter();

        // Act & Assert
        factory.CanConvert(typeof(TestColor)).ShouldBeTrue();
    }

    [Fact]
    public void CanConvert_Should_Return_False_For_Non_Enumeration()
    {
        // Arrange
        var factory = new JsonStringEnumerationConverter();

        // Act & Assert
        factory.CanConvert(typeof(string)).ShouldBeFalse();
    }

    [Fact]
    public void CreateConverter_Should_Return_Converter_For_Enumeration()
    {
        // Arrange
        var factory = new JsonStringEnumerationConverter();
        var options = new JsonSerializerOptions();

        // Act
        var converter = factory.CreateConverter(typeof(TestColor), options);

        // Assert
        converter.ShouldNotBeNull();
        converter.ShouldBeOfType<JsonStringSmartEnumConverter<TestColor>>();
    }
}
