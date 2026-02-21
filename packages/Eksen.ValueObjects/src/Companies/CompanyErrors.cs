using Eksen.Core;
using Eksen.Core.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

namespace Eksen.ValueObjects.Companies;

public static class CompanyErrors
{
    public static readonly string Category = $"{AppModules.ValueObjects}.Companies";

    public static readonly ErrorDescriptor EmptyCompanyName = new(
        ErrorType.Validation,
        Category
    );

    public static readonly ErrorDescriptor<ValueLengthOverflowError> CompanyNameOverflow = new(
        ErrorType.Validation,
        Category,
        self => (value, maxLength) =>
            new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxLength));

    public static readonly ErrorDescriptor EmptyTaxOffice = new(
        ErrorType.Validation,
        Category
    );

    public static readonly ErrorDescriptor<ValueLengthOverflowError> TaxOfficeOverflow = new(
        ErrorType.Validation,
        Category,
        self => (value, maxLength) =>
            new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxLength));
}