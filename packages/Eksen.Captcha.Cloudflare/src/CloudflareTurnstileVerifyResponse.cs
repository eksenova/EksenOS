using System.Text.Json.Serialization;

namespace Eksen.Captcha.Cloudflare;

internal sealed record CloudflareTurnstileVerifyResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("error-codes")]
    public string[]? ErrorCodes { get; init; }

    [JsonPropertyName("challenge_ts")]
    public DateTimeOffset? ChallengeTime { get; init; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; init; }

    [JsonPropertyName("action")]
    public string? Action { get; init; }
}
