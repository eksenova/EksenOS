using Eksen.Core;
using Eksen.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

namespace Eksen.Authentication.ApiKeys;

public static class ApiKeyErrors
{
    public static readonly string Category = AppModules.AuthenticationApiKeys;

    public static readonly ErrorDescriptor EmptyApiKeyValue =
        new(ErrorType.Validation, Category);

    public static readonly ErrorDescriptor<ValueLengthOverflowError> ApiKeyValueOverflow =
        new(ErrorType.Validation, Category,
            self => (value, maxLength) => new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxLength));

    public static readonly ErrorDescriptor EmptyApiKeyName =
        new(ErrorType.Validation, Category);

    public static readonly ErrorDescriptor<ValueLengthOverflowError> ApiKeyNameOverflow =
        new(ErrorType.Validation, Category,
            self => (value, maxLength) => new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxLength));

    public static readonly ErrorDescriptor ApiKeyRevoked =
        new(ErrorType.Authorization, Category);

    public static readonly ErrorDescriptor ApiKeyExpired =
        new(ErrorType.Authorization, Category);

    public static readonly ErrorDescriptor ApiKeyNotFound =
        new(ErrorType.NotFound, Category);

    public static readonly ErrorDescriptor ApiKeyAlreadyRevoked =
        new(ErrorType.Validation, Category);
}
