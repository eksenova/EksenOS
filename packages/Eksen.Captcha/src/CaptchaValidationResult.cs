using System.Collections.Immutable;

namespace Eksen.Captcha;

public sealed record CaptchaValidationResult
{
    public static readonly CaptchaValidationResult Bypassed = new()
    {
        IsValid = true,
        IsBypassed = true,
    };

    public required bool IsValid { get; init; }

    public bool IsBypassed { get; init; }

    public ImmutableArray<string> ErrorCodes { get; init; } = [];

    public string? Hostname { get; init; }

    public DateTimeOffset? ChallengeTime { get; init; }

    public static CaptchaValidationResult Success(string? hostname = null, DateTimeOffset? challengeTime = null)
    {
        return new CaptchaValidationResult
        {
            IsValid = true,
            Hostname = hostname,
            ChallengeTime = challengeTime,
        };
    }

    public static CaptchaValidationResult Failure(params IEnumerable<string> errorCodes)
    {
        return new CaptchaValidationResult
        {
            IsValid = false,
            ErrorCodes = [.. errorCodes],
        };
    }
}
