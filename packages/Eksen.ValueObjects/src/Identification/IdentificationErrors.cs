using Eksen.Core;
using Eksen.Core.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

namespace Eksen.ValueObjects.Identification;

public static class IdentificationErrors
{
    public static readonly string Category = $"{AppModules.ValueObjects}.Identification";

    public static readonly ErrorDescriptor EmptyLastName = new(
        ErrorType.Validation,
        Category);

    public static readonly ErrorDescriptor<ValueLengthOverflowError> LastNameOverflow = new(
        ErrorType.Validation,
        Category,
        self => (value, maxLength) =>
            new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxLength));

    public static readonly ErrorDescriptor EmptyFirstName = new(
        ErrorType.Validation,
        Category);

    public static readonly ErrorDescriptor<ValueLengthOverflowError> FirstNameOverflow = new(
        ErrorType.Validation,
        Category,
        self => (value, maxLength) =>
            new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxLength));

    public static readonly ErrorDescriptor EmptyFullName = new(
        ErrorType.Validation,
        Category);

    public static readonly ErrorDescriptor<ValueLengthOverflowError> FullNameOverflow = new(
        ErrorType.Validation,
        Category,
        self => (value, maxLength) =>
            new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxLength));
}