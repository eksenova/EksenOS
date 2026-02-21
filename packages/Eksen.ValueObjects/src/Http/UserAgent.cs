namespace Eksen.ValueObjects.Http;

public sealed record UserAgent
{
    public const int MaxLength = 255;

    public string Value { get; }

    public UserAgent(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            throw HttpErrors.EmptyUserAgent.Raise();
        }

        if (userAgent.Length > MaxLength)
        {
            throw HttpErrors.UserAgentOverflow.Raise(userAgent, MaxLength);
        }

        Value = userAgent;
    }
}