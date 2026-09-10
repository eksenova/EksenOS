using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Eksen.Cloudflare;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.Captcha.Cloudflare;

public sealed class CloudflareTurnstileCaptchaProvider(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<CloudflareTurnstileOptions> optionsMonitor,
    ILogger<CloudflareTurnstileCaptchaProvider> logger
) : ICaptchaProvider
{
    private const string TransportFailureErrorCode = "eksen-transport-failure";

    public async Task<CaptchaValidationResult> VerifyAsync(
        CaptchaResponseToken responseToken,
        IPAddress? remoteIpAddress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responseToken);

        var options = optionsMonitor.CurrentValue;

        var fields = new Dictionary<string, string>
        {
            ["secret"] = options.SecretKey,
            ["response"] = responseToken.Value,
        };

        if (options.SendRemoteIpAddress && remoteIpAddress is not null)
        {
            fields["remoteip"] = CloudflareIpRanges.Normalize(remoteIpAddress).ToString();
        }

        using var httpClient = httpClientFactory.CreateClient(CloudflareDefaults.TurnstileHttpClientName);

        using var timeoutSource = new CancellationTokenSource(options.VerifyTimeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        try
        {
            using var content = new FormUrlEncodedContent(fields);
            using var response = await httpClient.PostAsync(options.VerifyUrl, content, linkedSource.Token);

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<CloudflareTurnstileVerifyResponse>(
                linkedSource.Token);

            if (payload is null)
            {
                return CaptchaValidationResult.Failure(TransportFailureErrorCode);
            }

            if (!payload.Success)
            {
                return CaptchaValidationResult.Failure(payload.ErrorCodes ?? []);
            }

            return CaptchaValidationResult.Success(payload.Hostname, payload.ChallengeTime);
        }
        catch (Exception exception) when (exception is not OperationCanceledException
                                          || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Cloudflare Turnstile verification could not be completed.");
            return CaptchaValidationResult.Failure(TransportFailureErrorCode);
        }
    }
}
