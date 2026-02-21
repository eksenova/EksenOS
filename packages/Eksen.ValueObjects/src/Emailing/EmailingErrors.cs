using Eksen.Core;
using Eksen.Core.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

namespace Eksen.ValueObjects.Emailing;

public static class EmailingErrors
{
    public static readonly string Category = $"{AppModules.ValueObjects}.Emailing";

    public static readonly ErrorDescriptor EmptyEmailAddress = new(
        ErrorType.Validation,
        Category);

    public static readonly ErrorDescriptor<ValueLengthOverflowError> EmailAddressOverflow = new(
        ErrorType.Validation,
        Category,
        self => (value, maxValue) =>
            new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxValue));

    public static readonly ErrorDescriptor<ValueParseError> InvalidEmailAddress = new(
        ErrorType.Validation,
        Category,
        self => value =>
            new ErrorInstance(self)
                .WithValue(value));
}