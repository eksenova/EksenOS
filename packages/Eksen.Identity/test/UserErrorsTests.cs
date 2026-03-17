using Eksen.ErrorHandling;
using Eksen.Identity.Users;
using Eksen.TestBase;
using Eksen.ValueObjects.Emailing;
using Shouldly;

namespace Eksen.Identity.Tests;

public class UserErrorsTests : EksenUnitTestBase
{
    [Fact]
    public void Category_Should_Contain_Users()
    {
        UserErrors.Category.ShouldEndWith(".Users");
    }

    [Fact]
    public void EmptyPassword_Should_Be_Validation_Error()
    {
        UserErrors.EmptyPassword.ErrorType.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void ShortPassword_Should_Be_Validation_Error()
    {
        UserErrors.ShortPassword.ErrorType.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void WeakPassword_Should_Be_Validation_Error()
    {
        UserErrors.WeakPassword.ErrorType.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void EmailAddressAlreadyExists_Should_Be_Validation_Error()
    {
        UserErrors.EmailAddressAlreadyExists.ErrorType.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void EmailAddressAlreadyExists_Should_Raise_ErrorInstance_With_EmailAddress()
    {
        // Arrange
        var email = EmailAddress.Parse("test@example.com");

        // Act
        var error = UserErrors.EmailAddressAlreadyExists.Raise(email);

        // Assert
        error.ShouldNotBeNull();
        error.Descriptor.ShouldBe(UserErrors.EmailAddressAlreadyExists);
    }
}
