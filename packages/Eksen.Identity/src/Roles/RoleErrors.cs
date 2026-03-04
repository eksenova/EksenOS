using Eksen.Core;
using Eksen.Entities;
using Eksen.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

namespace Eksen.Identity.Roles;

public static class RoleErrors
{
    public static readonly string Category = $"{AppModules.Entities}.Roles";

    public static readonly ErrorDescriptor RoleNameEmpty = new(
        ErrorType.Validation,
        Category);

    public static readonly ErrorDescriptor<ValueLengthOverflowError> RoleNameOverflow = new(
        ErrorType.Validation,
        Category,
        self => (value, maxLength) =>
            new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxLength));

    public delegate ErrorInstance RoleNameAlreadyExistsError(RoleName roleName);

    public static readonly ErrorDescriptor<RoleNameAlreadyExistsError> RoleNameAlreadyExists = new(
        ErrorType.Validation,
        Category,
        self => roleName =>
            new ErrorInstance(self)
                .WithValue(roleName.Value, nameof(roleName)));

    public static readonly ErrorDescriptor CannotDeleteWithUsers = new(
        ErrorType.Validation,
        Category);
}