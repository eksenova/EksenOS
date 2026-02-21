using Eksen.Core;
using Eksen.Core.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

namespace Eksen.Entities.Roles;

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

    public static readonly ErrorDescriptor RoleNameAlreadyExists = new(
        ErrorType.Conflict,
        Category);
}