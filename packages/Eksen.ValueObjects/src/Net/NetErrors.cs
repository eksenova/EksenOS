using Eksen.Core;
using Eksen.Core.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

namespace Eksen.ValueObjects.Net;

public static class NetErrors
{
    public static readonly string Category = $"{AppModules.ValueObjects}.Net";

    public static readonly ErrorDescriptor EmptyIpAddress = new(
        ErrorType.Validation,
        Category);

    public static readonly ErrorDescriptor<ValueLengthOverflowError> IpV4AddressOverflow = new(
        ErrorType.Validation,
        Category,
        self => (value, maxValue) =>
            new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxValue));

    public static readonly ErrorDescriptor<ValueParseError> InvalidIpV4Address = new(
        ErrorType.Validation,
        Category,
        self => value =>
            new ErrorInstance(self)
                .WithValue(value));


    public static readonly ErrorDescriptor<ValueParseError> InvalidPort = new(
        ErrorType.Validation,
        Category,
        self => value =>
            new ErrorInstance(self)
                .WithValue(value));
}

