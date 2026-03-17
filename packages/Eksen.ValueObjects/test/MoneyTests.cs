using Eksen.ErrorHandling;
using Eksen.TestBase;
using Eksen.ValueObjects.Finance;
using Shouldly;

namespace Eksen.ValueObjects.Tests;

public class MoneyTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Be_Successful()
    {
        // Arrange
        var currency = Currency.Usd;
        var amount = MoneyAmount.Create(100m);

        // Act
        var money = Money.Create((currency, amount));

        // Assert
        money.Currency.ShouldBe(Currency.Usd);
        money.Amount.ShouldBe(amount);
    }

    [Fact]
    public void Parse_Should_Return_Money()
    {
        // Arrange & Act
        var money = Money.Parse("100.00 USD");

        // Assert
        money.Currency.ShouldBe(Currency.Usd);
        money.Amount.Value.ShouldBe(100.00m);
    }

    [Fact]
    public void Parse_Should_Handle_EUR_Currency()
    {
        // Arrange & Act
        var money = Money.Parse("50.25 EUR");

        // Assert
        money.Currency.ShouldBe(Currency.Eur);
        money.Amount.Value.ShouldBe(50.25m);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("100")]
    [InlineData("100 USD EXTRA")]
    public void Parse_Should_Throw_When_Invalid_Format(string value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => Money.Parse(value));
        exception.Descriptor.ShouldBe(FinanceErrors.InvalidMoneyFormat);
    }

    [Fact]
    public void ToParseableString_Should_Return_Formatted_String()
    {
        // Arrange
        var money = Money.Create((Currency.Usd, MoneyAmount.Create(100.5m)));

        // Act
        var result = money.ToParseableString(System.Globalization.CultureInfo.InvariantCulture);

        // Assert
        result.ShouldContain("100.50");
        result.ShouldContain("USD");
    }
}
