using System.Net;

namespace Eksen.Captcha;

public interface ICaptchaValidator
{
    Task<CaptchaValidationResult> ValidateAsync(
        CaptchaResponseToken responseToken,
        IPAddress? remoteIpAddress = null,
        CancellationToken cancellationToken = default);
}
