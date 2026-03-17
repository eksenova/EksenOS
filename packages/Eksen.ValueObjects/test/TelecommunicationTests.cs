using Eksen.ErrorHandling;
using Eksen.TestBase;
using Eksen.ValueObjects.Telecommunications;
using Shouldly;

namespace Eksen.ValueObjects.Tests;

public class PhoneNumberTests : EksenUnitTestBase
{
    [Theory]
    [InlineData("+905551234567")]
    [InlineData("0555 123 4567")]
    [InlineData("+1-202-555-0173")]
    [InlineData("(212) 555-0199")]
    public void Create_Should_Be_Successful(string value)
    {
        // Arrange & Act
        var phone = PhoneNumber.Create(value);

        // Assert
        phone.Value.ShouldBe(value.Trim());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Null_Or_Whitespace(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => PhoneNumber.Create(value!));
        exception.Descriptor.ShouldBe(TelecommunicationErrors.EmptyPhoneNumber);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longValue = "+" + new string('0', PhoneNumber.MaxLength);

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => PhoneNumber.Create(longValue));
        exception.Descriptor.ShouldBe(TelecommunicationErrors.PhoneNumberOverflow);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12")]
    public void Create_Should_Throw_When_Invalid_Format(string value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => PhoneNumber.Create(value));
        exception.Descriptor.ShouldBe(TelecommunicationErrors.InvalidPhoneNumber);
    }

    [Fact]
    public void MaxLength_Should_Be_20()
    {
        // Assert
        PhoneNumber.MaxLength.ShouldBe(20);
    }

    [Fact]
    public void ToParseableString_Should_Return_Value()
    {
        // Arrange
        var phone = PhoneNumber.Create("+905551234567");

        // Act
        var result = phone.ToParseableString();

        // Assert
        result.ShouldBe("+905551234567");
    }
}

public class GsmPhoneNumberTests : EksenUnitTestBase
{
    [Theory]
    [InlineData("+905551234567")]
    [InlineData("05551234567")]
    [InlineData("+1-202-555-0173")]
    public void Create_Should_Be_Successful(string value)
    {
        // Arrange & Act
        var phone = GsmPhoneNumber.Create(value);

        // Assert
        phone.Value.ShouldBe(value.Trim());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Null_Or_Whitespace(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => GsmPhoneNumber.Create(value!));
        exception.Descriptor.ShouldBe(TelecommunicationErrors.EmptyGsmPhoneNumber);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longValue = "+" + new string('0', GsmPhoneNumber.MaxLength);

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => GsmPhoneNumber.Create(longValue));
        exception.Descriptor.ShouldBe(TelecommunicationErrors.GsmPhoneNumberOverflow);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12345")]
    public void Create_Should_Throw_When_Invalid_Format(string value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => GsmPhoneNumber.Create(value));
        exception.Descriptor.ShouldBe(TelecommunicationErrors.InvalidGsmPhoneNumber);
    }

    [Fact]
    public void MaxLength_Should_Be_20()
    {
        // Assert
        GsmPhoneNumber.MaxLength.ShouldBe(20);
    }
}
