using System.Text.RegularExpressions;

namespace Eksen.Telemetry.Redaction;

/// <summary>
/// Redacts one named telemetry value (a log attribute, a scope field or a span tag): values under a sensitive
/// name are replaced entirely, other text has its sensitive query values and body fields redacted and its e-mail
/// addresses replaced with <see cref="RedactedEmail"/>.
/// </summary>
public static partial class TelemetryValueRedactor
{
    public const string RedactedValue = SensitiveBodyRedactor.RedactedValue;

    /// <summary>
    /// The longest text <see cref="RedactText"/> keeps. Longer text (an oversized header, message or stack trace)
    /// is cut to this length, followed by <see cref="TruncationMarker"/>, before it is redacted, which bounds the
    /// work every log record and scope costs.
    /// </summary>
    public const int MaxTextLength = 32 * 1024;

    public const string TruncationMarker = "...[truncated]";

    /// <summary>What an e-mail address in redacted text becomes.</summary>
    public const string RedactedEmail = "[email]";

    public static bool IsSensitiveName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (SensitiveFieldName.IsSensitive(name))
        {
            return true;
        }

        // Dotted names such as eksen.http.query.token are judged by their last segment.
        var lastDot = name.LastIndexOf('.');
        return lastDot >= 0 && lastDot < name.Length - 1 && SensitiveFieldName.IsSensitive(name[(lastDot + 1)..]);
    }

    public static string Redact(string? name, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (name is not null && IsSensitiveName(name))
        {
            return RedactedValue;
        }

        return RedactText(value);
    }

    /// <summary>
    /// Redacts one structured value: any value under a sensitive name becomes <see cref="RedactedValue"/>, string
    /// and <see cref="Uri"/> values pass through <see cref="RedactText"/>, other values are returned as they are.
    /// </summary>
    public static object? RedactValue(string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (value is null)
        {
            return null;
        }

        if (IsSensitiveName(name))
        {
            return RedactedValue;
        }

        return value switch
        {
            string text => RedactText(text),
            Uri uri => RedactText(uri.OriginalString),
            _ => value
        };
    }

    public static string RedactText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return RedactEmailAddresses(SensitiveBodyRedactor.Redact(SensitiveUrlRedactor.Redact(Truncate(value, MaxTextLength))));
    }

    /// <summary>
    /// Replaces every e-mail address in <paramref name="value"/> with <see cref="RedactedEmail"/>. Exception
    /// messages from SMTP, database and HTTP clients often echo addresses, and they reach logs and span events
    /// through <see cref="RedactText"/>.
    /// </summary>
    public static string RedactEmailAddresses(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.IndexOf('@') < 0)
        {
            return value;
        }

        try
        {
            return EmailAddress().Replace(value, RedactedEmail);
        }
        catch (RegexMatchTimeoutException)
        {
            return RedactedValue;
        }
    }

    // Starts only where a local part starts and matches each part atomically, so a long run with or without an @
    // is scanned once. The parts never overlap (the dot separates them), so the atomic groups change no match.
    [GeneratedRegex(pattern: @"(?<![\w.+-])(?>[\w.+-]+)@(?>[\w-]+)(?:\.(?>[\w-]+))+", RegexOptions.CultureInvariant, SensitiveFieldName.RegexTimeoutMilliseconds)]
    private static partial Regex EmailAddress();

    /// <summary>
    /// Returns <paramref name="value"/> itself when it is at most <paramref name="maxLength"/> characters long, or
    /// its first <paramref name="maxLength"/> characters followed by <see cref="TruncationMarker"/>.
    /// </summary>
    public static string Truncate(string value, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);

        if (value.Length <= maxLength)
        {
            return value;
        }

        // Do not split a surrogate pair.
        var length = maxLength > 0 && char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength;
        return string.Concat(value.AsSpan(0, length), TruncationMarker);
    }
}
