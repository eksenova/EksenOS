using Eksen.ErrorHandling;
using Eksen.TestBase;
using Eksen.ValueObjects.Measurements;
using Shouldly;

namespace Eksen.ValueObjects.Tests;

public class QuantityTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Be_Successful()
    {
        // Arrange & Act
        var quantity = Quantity.Create((10m, QuantityUnit.Piece));

        // Assert
        quantity.Amount.ShouldBe(10m);
        quantity.Unit.ShouldBe(QuantityUnit.Piece);
    }

    [Theory]
    [InlineData(0.0001)]
    [InlineData(1)]
    [InlineData(999_999_999)]
    public void Create_Should_Accept_Valid_Amounts(decimal amount)
    {
        // Arrange & Act
        var quantity = Quantity.Create((amount, QuantityUnit.Box));

        // Assert
        quantity.Amount.ShouldBe(amount);
    }

    [Fact]
    public void Create_Should_Throw_When_Amount_Too_Small()
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => Quantity.Create((0m, QuantityUnit.Piece)));
        exception.Descriptor.ShouldBe(MeasurementErrors.QuantityTooSmall);
    }

    [Fact]
    public void Create_Should_Throw_When_Amount_Too_Large()
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(
            () => Quantity.Create((Quantity.MaxValue + 1, QuantityUnit.Piece)));
        exception.Descriptor.ShouldBe(MeasurementErrors.QuantityTooLarge);
    }

    [Fact]
    public void Parse_Should_Return_Quantity()
    {
        // Arrange & Act
        var quantity = Quantity.Parse("5.0000 Piece");

        // Assert
        quantity.Amount.ShouldBe(5m);
        quantity.Unit.ShouldBe(QuantityUnit.Piece);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("5")]
    [InlineData("5 Piece Extra")]
    public void Parse_Should_Throw_When_Invalid_Format(string value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => Quantity.Parse(value));
        exception.Descriptor.ShouldBe(MeasurementErrors.InvalidQuantityFormat);
    }

    [Fact]
    public void Parse_Should_Throw_When_Amount_Not_Numeric()
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => Quantity.Parse("abc Piece"));
        exception.Descriptor.ShouldBe(MeasurementErrors.InvalidQuantityFormat);
    }

    [Fact]
    public void ToParseableString_Should_Return_Formatted_String()
    {
        // Arrange
        var quantity = Quantity.Create((5m, QuantityUnit.Piece));

        // Act
        var result = quantity.ToParseableString();

        // Assert
        result.ShouldContain("5.0000");
        result.ShouldContain("Piece");
    }
}

public class WeightTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Be_Successful()
    {
        // Arrange & Act
        var weight = Weight.Create((10m, WeightUnit.Kilogram));

        // Assert
        weight.Amount.ShouldBe(10m);
        weight.Unit.ShouldBe(WeightUnit.Kilogram);
    }

    [Fact]
    public void Create_Should_Accept_Zero()
    {
        // Arrange & Act
        var weight = Weight.Create((0m, WeightUnit.Kilogram));

        // Assert
        weight.Amount.ShouldBe(0m);
    }

    [Fact]
    public void Create_Should_Throw_When_Negative()
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(
            () => Weight.Create((-1m, WeightUnit.Kilogram)));
        exception.Descriptor.ShouldBe(MeasurementErrors.NegativeWeight);
    }

    [Fact]
    public void Create_Should_Throw_When_Too_Large()
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(
            () => Weight.Create((Weight.MaxValue + 1, WeightUnit.Kilogram)));
        exception.Descriptor.ShouldBe(MeasurementErrors.WeightTooLarge);
    }

    [Fact]
    public void ConvertTo_Should_Convert_Kilogram_To_Gram()
    {
        // Arrange
        var weight = Weight.Create((1m, WeightUnit.Kilogram));

        // Act
        var converted = weight.ConvertTo(WeightUnit.Gram);

        // Assert
        converted.Amount.ShouldBe(1000m);
        converted.Unit.ShouldBe(WeightUnit.Gram);
    }

    [Fact]
    public void ConvertTo_Should_Convert_Ton_To_Kilogram()
    {
        // Arrange
        var weight = Weight.Create((1m, WeightUnit.Ton));

        // Act
        var converted = weight.ConvertTo(WeightUnit.Kilogram);

        // Assert
        converted.Amount.ShouldBe(1000m);
        converted.Unit.ShouldBe(WeightUnit.Kilogram);
    }

    [Fact]
    public void ToKilograms_Should_Return_Correct_Value()
    {
        // Arrange
        var weight = Weight.Create((500m, WeightUnit.Gram));

        // Act
        var kg = weight.ToKilograms();

        // Assert
        kg.ShouldBe(0.5m);
    }

    [Fact]
    public void ToGrams_Should_Return_Correct_Value()
    {
        // Arrange
        var weight = Weight.Create((1m, WeightUnit.Kilogram));

        // Act
        var grams = weight.ToGrams();

        // Assert
        grams.ShouldBe(1000m);
    }

    [Fact]
    public void ToTons_Should_Return_Correct_Value()
    {
        // Arrange
        var weight = Weight.Create((1000m, WeightUnit.Kilogram));

        // Act
        var tons = weight.ToTons();

        // Assert
        tons.ShouldBe(1m);
    }

    [Fact]
    public void Parse_Should_Return_Weight()
    {
        // Arrange & Act
        var weight = Weight.Parse("5.0000 Kilogram");

        // Assert
        weight.Amount.ShouldBe(5m);
        weight.Unit.ShouldBe(WeightUnit.Kilogram);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("5")]
    [InlineData("5 Kilogram Extra")]
    public void Parse_Should_Throw_When_Invalid_Format(string value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => Weight.Parse(value));
        exception.Descriptor.ShouldBe(MeasurementErrors.InvalidWeightFormat);
    }

    [Fact]
    public void Parse_Should_Throw_When_Amount_Not_Numeric()
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => Weight.Parse("abc Kilogram"));
        exception.Descriptor.ShouldBe(MeasurementErrors.InvalidWeightFormat);
    }

    [Fact]
    public void ToParseableString_Should_Return_Formatted_String()
    {
        // Arrange
        var weight = Weight.Create((5m, WeightUnit.Kilogram));

        // Act
        var result = weight.ToParseableString();

        // Assert
        result.ShouldContain("5.0000");
        result.ShouldContain("Kilogram");
    }
}
