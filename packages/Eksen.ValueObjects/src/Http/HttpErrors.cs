using Eksen.Core;
using Eksen.Core.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

namespace Eksen.ValueObjects.Http;

public static class HttpErrors
{
    public static readonly string Category = $"{AppModules.ValueObjects}.Web";

    public static readonly ErrorDescriptor EmptyUserAgent = new(
        ErrorType.Validation,
        Category);

    public static readonly ErrorDescriptor<ValueLengthOverflowError> UserAgentOverflow = new(
        ErrorType.Validation,
        Category,
        self => (value, maxLength) =>
            new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxLength));
}

