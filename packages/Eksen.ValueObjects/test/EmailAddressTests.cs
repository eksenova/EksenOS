using Eksen.ErrorHandling;
using Eksen.TestBase;
using Eksen.ValueObjects.Emailing;
using Shouldly;

namespace Eksen.ValueObjects.Tests;

public class EmailAddressTests : EksenUnitTestBase
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("test.user@domain.org")]
    [InlineData("name+tag@company.co")]
    public void Create_Should_Be_Successful(string email)
    {
        // Arrange & Act
        var emailAddress = EmailAddress.Create(email);

        // Assert
        emailAddress.Value.ShouldBe(email.ToLowerInvariant());
    }

    [Fact]
    public void Create_Should_Normalize_To_Lowercase()
    {
        // Arrange & Act
        var emailAddress = EmailAddress.Create("User@Example.COM");

        // Assert
        emailAddress.Value.ShouldBe("user@example.com");
    }

    [Fact]
    public void Create_Should_Trim_Whitespace()
    {
        // Arrange & Act
        var emailAddress = EmailAddress.Create("  user@example.com  ");

        // Assert
        emailAddress.Value.ShouldBe("user@example.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Null_Or_Whitespace(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => EmailAddress.Create(value!));
        exception.Descriptor.ShouldBe(EmailingErrors.EmptyEmailAddress);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longEmail = new string('a', EmailAddress.MaxLength) + "@example.com";

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => EmailAddress.Create(longEmail));
        exception.Descriptor.ShouldBe(EmailingErrors.EmailAddressOverflow);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("missing@")]
    [InlineData("@domain.com")]
    [InlineData("no spaces@domain.com")]
    public void Create_Should_Throw_When_Invalid_Format(string email)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => EmailAddress.Create(email));
        exception.Descriptor.ShouldBe(EmailingErrors.InvalidEmailAddress);
    }

    [Fact]
    public void MaxLength_Should_Be_50()
    {
        // Assert
        EmailAddress.MaxLength.ShouldBe(50);
    }

    [Fact]
    public void Parse_Should_Return_EmailAddress()
    {
        // Arrange & Act
        var emailAddress = EmailAddress.Parse("user@example.com");

        // Assert
        emailAddress.Value.ShouldBe("user@example.com");
    }

    [Fact]
    public void ToParseableString_Should_Return_Value()
    {
        // Arrange
        var emailAddress = EmailAddress.Create("user@example.com");

        // Act
        var result = emailAddress.ToParseableString();

        // Assert
        result.ShouldBe("user@example.com");
    }

    [Fact]
    public void TryParse_Should_Return_True_For_Valid_Email()
    {
        // Arrange & Act
        var success = ValueObject<EmailAddress, string>.TryParse("user@example.com", out var result);

        // Assert
        success.ShouldBeTrue();
        result.ShouldNotBeNull();
        result.Value.ShouldBe("user@example.com");
    }

    [Fact]
    public void TryParse_Should_Return_False_For_Invalid_Email()
    {
        // Arrange & Act
        var success = ValueObject<EmailAddress, string>.TryParse("invalid", out var result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void Equal_EmailAddresses_Should_Be_Equal()
    {
        // Arrange
        var email1 = EmailAddress.Create("user@example.com");
        var email2 = EmailAddress.Create("USER@EXAMPLE.COM");

        // Assert
        email1.ShouldBe(email2);
    }
}
