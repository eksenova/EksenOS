using Eksen.ErrorHandling;
using Eksen.TestBase;
using Eksen.ValueObjects.Finance;
using Shouldly;

namespace Eksen.ValueObjects.Tests;

public class DiscountRateTests : EksenUnitTestBase
{
    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void Create_Should_Be_Successful(decimal value)
    {
        // Arrange & Act
        var rate = DiscountRate.Create(value);

        // Assert
        rate.Value.ShouldBe(value);
    }

    [Fact]
    public void Zero_Should_Return_Zero_Rate()
    {
        // Arrange & Act
        var zero = DiscountRate.Zero;

        // Assert
        zero.Value.ShouldBe(0m);
    }

    [Fact]
    public void Create_Should_Throw_When_Negative()
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => DiscountRate.Create(-1m));
        exception.Descriptor.ShouldBe(FinanceErrors.NegativeDiscountRate);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxValue()
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => DiscountRate.Create(101m));
        exception.Descriptor.ShouldBe(FinanceErrors.DiscountRateOverflow);
    }

    [Fact]
    public void Parse_Should_Return_DiscountRate()
    {
        // Arrange & Act
        var rate = DiscountRate.Parse("25.50");

        // Assert
        rate.Value.ShouldBe(25.50m);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("not-a-number")]
    public void Parse_Should_Throw_When_Invalid(string value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => DiscountRate.Parse(value));
        exception.Descriptor.ShouldBe(FinanceErrors.InvalidDiscountRate);
    }

    [Fact]
    public void Implicit_Conversion_To_Decimal_Should_Work()
    {
        // Arrange
        var rate = DiscountRate.Create(25m);

        // Act
        decimal value = rate;

        // Assert
        value.ShouldBe(25m);
    }

    [Fact]
    public void Explicit_Conversion_From_Decimal_Should_Work()
    {
        // Arrange & Act
        var rate = (DiscountRate)25m;

        // Assert
        rate.Value.ShouldBe(25m);
    }

    [Fact]
    public void ToParseableString_Should_Return_F2_Format()
    {
        // Arrange
        var rate = DiscountRate.Create(25.5m);

        // Act
        var result = rate.ToParseableString();

        // Assert
        result.ShouldBe("25.50");
    }
}

public class TaxRateTests : EksenUnitTestBase
{
    [Theory]
    [InlineData(0)]
    [InlineData(18)]
    [InlineData(100)]
    public void Create_Should_Be_Successful(decimal value)
    {
        // Arrange & Act
        var rate = TaxRate.Create(value);

        // Assert
        rate.Value.ShouldBe(value);
    }

    [Fact]
    public void Zero_Should_Return_Zero_Rate()
    {
        // Arrange & Act
        var zero = TaxRate.Zero;

        // Assert
        zero.Value.ShouldBe(0m);
    }

    [Fact]
    public void Create_Should_Throw_When_Negative()
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => TaxRate.Create(-1m));
        exception.Descriptor.ShouldBe(FinanceErrors.NegativeTaxRate);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxValue()
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => TaxRate.Create(101m));
        exception.Descriptor.ShouldBe(FinanceErrors.TaxRateOverflow);
    }

    [Fact]
    public void Parse_Should_Return_TaxRate()
    {
        // Arrange & Act
        var rate = TaxRate.Parse("18.00");

        // Assert
        rate.Value.ShouldBe(18.00m);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("not-a-number")]
    public void Parse_Should_Throw_When_Invalid(string value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => TaxRate.Parse(value));
        exception.Descriptor.ShouldBe(FinanceErrors.InvalidTaxRate);
    }

    [Fact]
    public void Implicit_Conversion_To_Decimal_Should_Work()
    {
        // Arrange
        var rate = TaxRate.Create(18m);

        // Act
        decimal value = rate;

        // Assert
        value.ShouldBe(18m);
    }

    [Fact]
    public void ToParseableString_Should_Return_F2_Format()
    {
        // Arrange
        var rate = TaxRate.Create(18.5m);

        // Act
        var result = rate.ToParseableString();

        // Assert
        result.ShouldBe("18.50");
    }
}
