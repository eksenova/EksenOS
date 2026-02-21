using Eksen.Core;
using Eksen.Core.ErrorHandling;
using Eksen.ValueObjects.Emailing;

namespace Eksen.Entities.Users;

public static class UserErrors
{
    public static readonly string Category = $"{AppModules.Entities}.Users";

    public static readonly ErrorDescriptor EmptyPassword = new(
        ErrorType.Validation,
        Category
    );

    public static readonly ErrorDescriptor ShortPassword = new(
        ErrorType.Validation,
        Category
    );

    public static readonly ErrorDescriptor WeakPassword = new(
        ErrorType.Validation,
        Category
    );

    public delegate ErrorInstance UserEmailAddressAlreadyTaken(EmailAddress emailAddress);

    public static readonly ErrorDescriptor<UserEmailAddressAlreadyTaken> EmailAddressAlreadyExists = new(
        ErrorType.Conflict,
        Category,
        self => emailAddress =>
            new ErrorInstance(self)
                .WithValue(emailAddress.Value)
    );
}