using Eksen.Core;
using Eksen.Entities;
using Eksen.ErrorHandling;
using Eksen.ValueObjects.Emailing;

namespace Eksen.Identity.Users;

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
        ErrorType.Validation,
        Category,
        self => emailAddress =>
            new ErrorInstance(self)
                .WithValue(emailAddress.Value, nameof(emailAddress))
    );
}