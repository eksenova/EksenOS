using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Eksen.Captcha;

public sealed class BypassAwareCaptchaValidator(
    ICaptchaProvider captchaProvider,
    IOptionsMonitor<EksenCaptchaOptions> optionsMonitor
) : ICaptchaValidator
{
    public async Task<CaptchaValidationResult> ValidateAsync(
        CaptchaResponseToken responseToken,
        IPAddress? remoteIpAddress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responseToken);

        var options = optionsMonitor.CurrentValue;

        if (!options.Enabled)
        {
            return CaptchaValidationResult.Bypassed;
        }

        if (IsBypassToken(options.BypassTokens, responseToken.Value))
        {
            return CaptchaValidationResult.Bypassed;
        }

        return await captchaProvider.VerifyAsync(responseToken, remoteIpAddress, cancellationToken);
    }

    internal static bool IsBypassToken(IList<string> bypassTokens, string candidate)
    {
        var candidateBytes = Encoding.UTF8.GetBytes(candidate);
        var matched = false;

        foreach (var bypassToken in bypassTokens)
        {
            if (string.IsNullOrEmpty(bypassToken))
            {
                continue;
            }

            var bypassBytes = Encoding.UTF8.GetBytes(bypassToken);

            if (CryptographicOperations.FixedTimeEquals(candidateBytes, bypassBytes))
            {
                matched = true;
            }
        }

        return matched;
    }
}
