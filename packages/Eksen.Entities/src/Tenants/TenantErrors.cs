using Eksen.Core;
using Eksen.Core.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

namespace Eksen.Entities.Tenants;

public static class TenantErrors
{
    public static readonly string Category = $"{AppModules.Entities}.Tenants";

    public static readonly ErrorDescriptor EmptyTenantName = new(
        ErrorType.Validation,
        Category
    );

    public static readonly ErrorDescriptor<ValueLengthOverflowError> TenantNameOverflow = new(
        ErrorType.Validation,
        Category,
        self => (value, maxLength) =>
            new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxLength)
    );
}