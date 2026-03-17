using Eksen.ErrorHandling;
using Eksen.TestBase;
using Eksen.ValueObjects.Finance;
using Shouldly;

namespace Eksen.ValueObjects.Tests;

public class IbanTests : EksenUnitTestBase
{
    [Theory]
    [InlineData("TR330006100519786457841326")]
    [InlineData("GB29NWBK60161331926819")]
    [InlineData("DE89370400440532013000")]
    public void Create_Should_Be_Successful(string value)
    {
        // Arrange & Act
        var iban = Iban.Create(value);

        // Assert
        iban.Value.ShouldBe(value.ToUpperInvariant().Replace(" ", ""));
    }

    [Fact]
    public void Create_Should_Normalize_To_Uppercase_And_Remove_Spaces()
    {
        // Arrange & Act
        var iban = Iban.Create("tr33 0006 1005 1978 6457 8413 26");

        // Assert
        iban.Value.ShouldBe("TR330006100519786457841326");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Null_Or_Whitespace(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => Iban.Create(value!));
        exception.Descriptor.ShouldBe(FinanceErrors.EmptyIban);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longIban = "TR" + "00" + new string('A', Iban.MaxLength);

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => Iban.Create(longIban));
        exception.Descriptor.ShouldBe(FinanceErrors.IbanOverflow);
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("12345678")]
    [InlineData("XX")]
    public void Create_Should_Throw_When_Invalid_Format(string value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => Iban.Create(value));
        exception.Descriptor.ShouldBe(FinanceErrors.InvalidIban);
    }

    [Fact]
    public void MaxLength_Should_Be_34()
    {
        // Assert
        Iban.MaxLength.ShouldBe(34);
    }

    [Fact]
    public void ToParseableString_Should_Return_Value()
    {
        // Arrange
        var iban = Iban.Create("GB29NWBK60161331926819");

        // Act
        var result = iban.ToParseableString();

        // Assert
        result.ShouldBe("GB29NWBK60161331926819");
    }
}
