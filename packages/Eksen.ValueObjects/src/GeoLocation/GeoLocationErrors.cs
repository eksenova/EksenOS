using Eksen.Core;
using Eksen.Core.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

namespace Eksen.ValueObjects.GeoLocation;

public static class GeoLocationErrors
{
    public static readonly string Category = $"{AppModules.ValueObjects}.GeoLocation";

    public static readonly ErrorDescriptor EmptyAddressLine = new(
        ErrorType.Validation,
        Category
    );

    public static readonly ErrorDescriptor<ValueLengthOverflowError> AddressLineOverflow = new(
        ErrorType.Validation,
        Category,
        self => (value, maxLength) =>
            new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxLength));
}