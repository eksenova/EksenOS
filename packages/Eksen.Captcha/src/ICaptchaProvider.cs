using System.Net;

namespace Eksen.Captcha;

public interface ICaptchaProvider
{
    Task<CaptchaValidationResult> VerifyAsync(
        CaptchaResponseToken responseToken,
        IPAddress? remoteIpAddress = null,
        CancellationToken cancellationToken = default);
}
