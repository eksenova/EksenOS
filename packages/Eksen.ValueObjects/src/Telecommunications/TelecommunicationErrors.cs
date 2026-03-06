using Eksen.Core;
using Eksen.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

namespace Eksen.ValueObjects.Telecommunications;

public static class TelecommunicationErrors
{
    public static readonly string Category = $"{AppModules.ValueObjects}.Telecommunications";

    public static readonly ErrorDescriptor EmptyPhoneNumber = new(
        ErrorType.Validation,
        Category
    );

    public static readonly ErrorDescriptor<ValueLengthOverflowError> PhoneNumberOverflow = new(
        ErrorType.Validation,
        Category,
        self => (value, maxLength) =>
            new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxLength));

    public static readonly ErrorDescriptor<ValueParseError> InvalidPhoneNumber = new(
        ErrorType.Validation,
        Category,
        self => value =>
            new ErrorInstance(self)
                .WithValue(value));

    public static readonly ErrorDescriptor EmptyGsmPhoneNumber = new(
        ErrorType.Validation,
        Category
    );

    public static readonly ErrorDescriptor<ValueLengthOverflowError> GsmPhoneNumberOverflow = new(
        ErrorType.Validation,
        Category,
        self => (value, maxLength) =>
            new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxLength));

    public static readonly ErrorDescriptor<ValueParseError> InvalidGsmPhoneNumber = new(
        ErrorType.Validation,
        Category,
        self => value =>
            new ErrorInstance(self)
                .WithValue(value));
}
