namespace Eksen.Telemetry.Redaction;

/// <summary>
/// Masks the values of credential-bearing HTTP headers with <c>***</c>. A header is sensitive when it is on the
/// default list or the configured <c>Telemetry:RedactedHeaders</c>, when its name is a sensitive field name (see
/// <see cref="TelemetryValueRedactor.IsSensitiveName"/>), or when one of its <c>-</c>/<c>_</c>-separated segments
/// is a credential word such as <c>key</c>, <c>secret</c>, <c>token</c>, <c>password</c> or <c>session</c>
/// (for example <c>X-Discord-Bot-Key</c>).
/// </summary>
public sealed class HeaderRedactor
{
    public const string RedactedValue = "***";

    public static readonly IReadOnlyList<string> DefaultSensitiveHeaders =
    [
        "Authorization",
        "Proxy-Authorization",
        "WWW-Authenticate",
        "Proxy-Authenticate",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key",
        "Api-Key",
        "X-XSRF-TOKEN",
        "X-CSRF-TOKEN",
        "X-Auth-Token",
        "X-Access-Token",
        "X-Refresh-Token",
        "X-Subscription-Key",
        "Ocp-Apim-Subscription-Key"
    ];

    public static readonly IReadOnlyList<string> SensitiveNameSegments =
    [
        "key",
        "apikey",
        "secret",
        "token",
        "password",
        "passwd",
        "pwd",
        "session",
        "credential",
        "credentials"
    ];

    private static readonly HashSet<string> SensitiveSegments = new(SensitiveNameSegments, StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _sensitiveHeaders;

    public HeaderRedactor(IEnumerable<string>? additionalSensitiveHeaders = null)
    {
        _sensitiveHeaders = new HashSet<string>(DefaultSensitiveHeaders, StringComparer.OrdinalIgnoreCase);

        if (additionalSensitiveHeaders is null)
        {
            return;
        }

        foreach (var header in additionalSensitiveHeaders)
        {
            if (!string.IsNullOrWhiteSpace(header))
            {
                _sensitiveHeaders.Add(header.Trim());
            }
        }
    }

    public bool IsSensitive(string headerName)
    {
        if (string.IsNullOrEmpty(headerName))
        {
            return false;
        }

        return _sensitiveHeaders.Contains(headerName)
               || TelemetryValueRedactor.IsSensitiveName(headerName)
               || HasSensitiveSegment(headerName);
    }

    public string Redact(string headerName, string value)
    {
        return IsSensitive(headerName)
            ? RedactedValue
            : value;
    }

    private static bool HasSensitiveSegment(string headerName)
    {
        foreach (var range in headerName.AsSpan().SplitAny('-', '_'))
        {
            var segment = headerName[range];
            if (segment.Length > 0 && SensitiveSegments.Contains(segment))
            {
                return true;
            }
        }

        return false;
    }
}
