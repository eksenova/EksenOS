using Eksen.Core;
using Eksen.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

namespace Eksen.ValueObjects.Measurements;

public static class MeasurementErrors
{
    public static readonly string Category = $"{AppModules.ValueObjects}.Measurements";

    public static readonly ErrorDescriptor<ValueValidationError<decimal>> QuantityTooSmall = new(
        ErrorType.Validation,
        Category,
        self => amount =>
            new ErrorInstance(self)
                .WithValue(amount));

    public static readonly ErrorDescriptor<ValueOverflowError<decimal>> QuantityTooLarge = new(
        ErrorType.Validation,
        Category,
        self => (amount, maxValue) =>
            new ErrorInstance(self)
                .WithValue(amount)
                .WithValue(maxValue));

    public static readonly ErrorDescriptor<ValueParseError> InvalidQuantityFormat = new(
        ErrorType.Validation,
        Category,
        self => value =>
            new ErrorInstance(self)
                .WithValue(value));

    public static readonly ErrorDescriptor<ValueValidationError<decimal>> NegativeWeight = new(
        ErrorType.Validation,
        Category,
        self => amount =>
            new ErrorInstance(self)
                .WithValue(amount));

    public static readonly ErrorDescriptor<ValueOverflowError<decimal>> WeightTooLarge = new(
        ErrorType.Validation,
        Category,
        self => (amount, maxValue) =>
            new ErrorInstance(self)
                .WithValue(amount)
                .WithValue(maxValue));

    public static readonly ErrorDescriptor<ValueParseError> InvalidWeightFormat = new(
        ErrorType.Validation,
        Category,
        self => value =>
            new ErrorInstance(self)
                .WithValue(value));
}
