using Eksen.Core;
using Eksen.Core.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

namespace Eksen.ValueObjects.Finance;

public static class FinanceErrors
{
    public static readonly string Category = $"{AppModules.ValueObjects}.Finance";

    public static readonly ErrorDescriptor<ValueOverflowError<decimal>> MoneyAmountOverflow
        = new(Category,
            ErrorType.Validation,
            self => (amount, maxValue) =>
                new ErrorInstance(self)
                    .WithValue(maxValue)
                    .WithValue(amount));

    public static readonly ErrorDescriptor<ValueValidationError<decimal>> NegativeMoneyAmount
        = new(Category,
            ErrorType.Validation,
            self => amount =>
                new ErrorInstance(self)
                    .WithValue(amount));

    public static readonly ErrorDescriptor<ValueParseError> InvalidMoneyAmount =
        new(Category,
            ErrorType.Validation,
            self => value =>
                new ErrorInstance(self)
                    .WithValue(value));

    public static readonly ErrorDescriptor<ValueValidationError<decimal>> MoneyAmountNotPositive =
        new(Category,
            ErrorType.Validation,
            self => amount =>
                new ErrorInstance(self)
                    .WithValue(amount));

    public static readonly ErrorDescriptor EmptyIban =
        new(Category,
            ErrorType.Validation);

    public static readonly ErrorDescriptor<ValueParseError> InvalidIban =
        new(Category,
            ErrorType.Validation,
            self => value =>
                new ErrorInstance(self)
                    .WithValue(value));

    public static readonly ErrorDescriptor<ValueLengthOverflowError> IbanOverflow = new(
        Category,
        ErrorType.Validation,
        self => (value, maxLength) =>
            new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxLength));
}

