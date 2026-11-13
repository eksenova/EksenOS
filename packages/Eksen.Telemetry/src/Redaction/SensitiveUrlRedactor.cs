using System.Text.RegularExpressions;

namespace Eksen.Telemetry.Redaction;

/// <summary>
/// Replaces the values of sensitive query parameters with <c>[redacted]</c>, anywhere in a text.
/// </summary>
/// <remarks>
/// The text is untrusted, so the pattern runs in linear time: a parameter name cannot contain <c>?</c> or
/// <c>&amp;</c>, so each candidate is scanned only up to the next separator. A redaction that still exceeds
/// <see cref="SensitiveFieldName.RegexTimeoutMilliseconds"/> replaces the whole value.
/// </remarks>
public static partial class SensitiveUrlRedactor
{
    public const string RedactedValue = "[redacted]";

    [GeneratedRegex(
        "([?&](?:amp;)?)((?>[^=?&\\s\"'<>]+))=([^&#\\s\"'<>]+)",
        RegexOptions.None,
        SensitiveFieldName.RegexTimeoutMilliseconds)]
    private static partial Regex QueryValue();

    public static string Redact(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        try
        {
            return QueryValue().Replace(value, match =>
                SensitiveFieldName.IsSensitive(SafeUnescape(match.Groups[2].Value))
                    ? match.Groups[1].Value + match.Groups[2].Value + "=" + RedactedValue
                    : match.Value);
        }
        catch (RegexMatchTimeoutException)
        {
            return RedactedValue;
        }
    }

    private static string SafeUnescape(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return value;
        }
    }
}
