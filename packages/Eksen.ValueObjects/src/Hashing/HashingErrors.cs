using Eksen.Core;
using Eksen.Core.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

namespace Eksen.ValueObjects.Hashing;

public static class HashingErrors
{
    public static readonly string Category = $"{AppModules.ValueObjects}.Hashing";

    public static readonly ErrorDescriptor EmptyHash = new(
        ErrorType.Validation,
        Category
    );

    public static readonly ErrorDescriptor<ValueLengthOverflowError> HashOverflow = new(
        ErrorType.Validation,
        Category,
        self => (value, maxLength) =>
            new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxLength));
}