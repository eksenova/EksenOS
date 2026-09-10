using Eksen.Core;
using Eksen.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

namespace Eksen.Captcha;

public static class CaptchaErrors
{
    public static readonly string Category = AppModules.Captcha;

    public static readonly ErrorDescriptor EmptyResponseToken = new(
        ErrorType.Validation,
        Category);

    public static readonly ErrorDescriptor<ValueLengthOverflowError> ResponseTokenOverflow = new(
        ErrorType.Validation,
        Category,
        self => (value, maxLength) =>
            new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxLength));

    public static readonly ErrorDescriptor VerificationFailed = new(
        ErrorType.Validation,
        Category);
}
