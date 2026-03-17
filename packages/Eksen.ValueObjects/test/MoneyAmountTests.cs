using Eksen.ErrorHandling;
using Eksen.TestBase;
using Eksen.ValueObjects.Finance;
using Shouldly;

namespace Eksen.ValueObjects.Tests;

public class MoneyAmountTests : EksenUnitTestBase
{
    [Theory]
    [InlineData(0)]
    [InlineData(1.5)]
    [InlineData(100)]
    [InlineData(999999)]
    public void Create_Should_Be_Successful(decimal value)
    {
        // Arrange & Act
        var amount = MoneyAmount.Create(value);

        // Assert
        amount.Value.ShouldBe(value);
    }

    [Fact]
    public void Zero_Should_Return_Zero_Amount()
    {
        // Arrange & Act
        var zero = MoneyAmount.Zero;

        // Assert
        zero.Value.ShouldBe(0m);
    }

    [Fact]
    public void Create_Should_Throw_When_Negative()
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => MoneyAmount.Create(-1m));
        exception.Descriptor.ShouldBe(FinanceErrors.NegativeMoneyAmount);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxValue()
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => MoneyAmount.Create(MoneyAmount.MaxValue + 1));
        exception.Descriptor.ShouldBe(FinanceErrors.MoneyAmountOverflow);
    }

    [Fact]
    public void Parse_Should_Return_MoneyAmount()
    {
        // Arrange & Act
        var amount = MoneyAmount.Parse("100.50");

        // Assert
        amount.Value.ShouldBe(100.50m);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("not-a-number")]
    public void Parse_Should_Throw_When_Invalid(string value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => MoneyAmount.Parse(value));
        exception.Descriptor.ShouldBe(FinanceErrors.InvalidMoneyAmount);
    }

    [Fact]
    public void Addition_Operator_Should_Add_Two_Amounts()
    {
        // Arrange
        var a = MoneyAmount.Create(10m);
        var b = MoneyAmount.Create(20m);

        // Act
        var result = a + b;

        // Assert
        result.Value.ShouldBe(30m);
    }

    [Fact]
    public void Subtraction_Operator_Should_Subtract_Two_Amounts()
    {
        // Arrange
        var a = MoneyAmount.Create(30m);
        var b = MoneyAmount.Create(10m);

        // Act
        var result = a - b;

        // Assert
        result.Value.ShouldBe(20m);
    }

    [Fact]
    public void Multiplication_Operator_Should_Multiply_Two_Amounts()
    {
        // Arrange
        var a = MoneyAmount.Create(5m);
        var b = MoneyAmount.Create(4m);

        // Act
        var result = a * b;

        // Assert
        result.Value.ShouldBe(20m);
    }

    [Fact]
    public void Division_Operator_Should_Divide_Two_Amounts()
    {
        // Arrange
        var a = MoneyAmount.Create(20m);
        var b = MoneyAmount.Create(4m);

        // Act
        var result = a / b;

        // Assert
        result.Value.ShouldBe(5m);
    }

    [Fact]
    public void Addition_With_Decimal_Should_Work()
    {
        // Arrange
        var a = MoneyAmount.Create(10m);

        // Act
        var result = a + 5m;

        // Assert
        result.Value.ShouldBe(15m);
    }

    [Fact]
    public void Multiplication_With_Uint_Should_Work()
    {
        // Arrange
        var a = MoneyAmount.Create(10m);

        // Act
        var result = a * 3u;

        // Assert
        result.Value.ShouldBe(30m);
    }

    [Fact]
    public void Implicit_Conversion_To_Decimal_Should_Work()
    {
        // Arrange
        var amount = MoneyAmount.Create(42.5m);

        // Act
        decimal value = amount;

        // Assert
        value.ShouldBe(42.5m);
    }

    [Fact]
    public void Explicit_Conversion_From_Decimal_Should_Work()
    {
        // Arrange & Act
        var amount = (MoneyAmount)42.5m;

        // Assert
        amount.Value.ShouldBe(42.5m);
    }

    [Fact]
    public void IsZero_Should_Return_True_When_Below_MinPositiveValue()
    {
        // Arrange
        var amount = MoneyAmount.Create(0m);

        // Assert
        amount.IsZero.ShouldBeTrue();
    }

    [Fact]
    public void IsZero_Should_Return_False_When_Above_MinPositiveValue()
    {
        // Arrange
        var amount = MoneyAmount.Create(1m);

        // Assert
        amount.IsZero.ShouldBeFalse();
    }

    [Fact]
    public void AssertPositive_Should_Throw_When_Zero()
    {
        // Arrange
        var amount = MoneyAmount.Zero;

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => amount.AssertPositive());
        exception.Descriptor.ShouldBe(FinanceErrors.MoneyAmountNotPositive);
    }

    [Fact]
    public void AssertPositive_Should_Not_Throw_When_Positive()
    {
        // Arrange
        var amount = MoneyAmount.Create(1m);

        // Act & Assert
        Should.NotThrow(() => amount.AssertPositive());
    }

    [Fact]
    public void Round_Should_Round_To_Two_Places_By_Default()
    {
        // Arrange & Act
        var result = MoneyAmount.Round(10.555m);

        // Assert
        result.ShouldBe(10.56m);
    }

    [Fact]
    public void ToParseableString_Should_Return_F4_Format()
    {
        // Arrange
        var amount = MoneyAmount.Create(100.5m);

        // Act
        var result = amount.ToParseableString();

        // Assert
        result.ShouldBe("100.5000");
    }
}
