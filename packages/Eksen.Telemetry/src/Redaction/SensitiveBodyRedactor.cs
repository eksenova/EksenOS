using System.Text;
using System.Text.RegularExpressions;

namespace Eksen.Telemetry.Redaction;

/// <summary>
/// Replaces the values of sensitive fields in JSON (also JSON serialized inside a string), XML elements,
/// XML attributes, CDATA sections and form bodies with <c>[redacted]</c>.
/// </summary>
/// <remarks>
/// The text is untrusted (request headers, bodies, exception messages), so every pattern runs in linear time:
/// a pattern matches a field's key with its name as one atomic token, <see cref="SensitiveFieldName.IsSensitive"/>
/// judges the name, and only a sensitive key has its value matched, from where the key ends. A redaction that still
/// exceeds <see cref="SensitiveFieldName.RegexTimeoutMilliseconds"/> replaces the whole value.
/// </remarks>
public static partial class SensitiveBodyRedactor
{
    public const string RedactedValue = "[redacted]";

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private const int Timeout = SensitiveFieldName.RegexTimeoutMilliseconds;
    private const string Name = SensitiveFieldName.NameToken;
    private const string Spaces = @"(?>\s*)";

    // A run of backslashes is matched from its first backslash only, so a long run is not rescanned from each one.
    private const string EscapedQuote = @"(?<!\\)(?>\\+)""";

    [GeneratedRegex(pattern: EscapedQuote + Name + EscapedQuote + Spaces + ":" + Spaces + EscapedQuote, Options, Timeout)]
    private static partial Regex EscapedJsonStringKey();

    // Up to the next run of backslashes followed by a quote.
    [GeneratedRegex(pattern: @"\G(?:[^\\]|(?>\\+)(?!""))*", Options | RegexOptions.Singleline, Timeout)]
    private static partial Regex EscapedJsonStringValue();

    [GeneratedRegex(pattern: "\"" + Name + "\"" + Spaces + ":" + Spaces + "\"", Options, Timeout)]
    private static partial Regex JsonStringKey();

    [GeneratedRegex(pattern: @"\G(?:[^""\\]|\\.)*", Options, Timeout)]
    private static partial Regex JsonStringValue();

    [GeneratedRegex(pattern: "\"" + Name + "\"" + Spaces + ":" + Spaces, Options, Timeout)]
    private static partial Regex JsonBareKey();

    [GeneratedRegex(pattern: @"\G(?![""{\[\s]|null\b)[^\s,}\]]+", Options, Timeout)]
    private static partial Regex JsonBareValue();

    [GeneratedRegex(pattern: @"<(?:(?>[\w.-]+):)?" + Name + @"(?:\s(?>[^<>]*))?(?<!/)>", Options, Timeout)]
    private static partial Regex XmlElementKey();

    [GeneratedRegex(pattern: @"\G(?:<!\[CDATA\[.*?(?:\]\]>|$)|[^<]*)", Options | RegexOptions.Singleline, Timeout)]
    private static partial Regex XmlElementValue();

    [GeneratedRegex(pattern: @"\s" + Name + Spaces + "=" + Spaces + "\"", Options, Timeout)]
    private static partial Regex XmlAttributeKey();

    [GeneratedRegex(pattern: @"\G[^""]*", Options, Timeout)]
    private static partial Regex XmlAttributeValue();

    [GeneratedRegex(pattern: @"\A" + Name + "=", Options, Timeout)]
    private static partial Regex LeadingFormKey();

    [GeneratedRegex(pattern: "&" + Name + "=", Options, Timeout)]
    private static partial Regex FollowingFormKey();

    [GeneratedRegex(pattern: @"\G[^&\s]*", Options, Timeout)]
    private static partial Regex FormValue();

    public static string Redact(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        try
        {
            return RedactFields(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return RedactedValue;
        }
    }

    /// <summary>
    /// Redacts a captured body. Form bodies (<c>application/x-www-form-urlencoded</c>) also have every
    /// sensitive field after the first one redacted.
    /// </summary>
    public static string Redact(string value, string? contentType)
    {
        ArgumentNullException.ThrowIfNull(value);

        try
        {
            var redacted = RedactFields(value);

            if (contentType is not null
                && contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                redacted = ReplaceSensitiveValues(redacted, FollowingFormKey(), FormValue(), RedactedValue);
            }

            return redacted;
        }
        catch (RegexMatchTimeoutException)
        {
            return RedactedValue;
        }
    }

    private static string RedactFields(string value)
    {
        var redacted = ReplaceSensitiveValues(value, EscapedJsonStringKey(), EscapedJsonStringValue(), RedactedValue);

        redacted = ReplaceSensitiveValues(redacted, JsonStringKey(), JsonStringValue(), RedactedValue);
        redacted = ReplaceSensitiveValues(redacted, JsonBareKey(), JsonBareValue(), "\"" + RedactedValue + "\"");
        redacted = ReplaceSensitiveValues(redacted, XmlElementKey(), XmlElementValue(), RedactedValue);
        redacted = ReplaceSensitiveValues(redacted, XmlAttributeKey(), XmlAttributeValue(), RedactedValue);

        return ReplaceSensitiveValues(redacted, LeadingFormKey(), FormValue(), RedactedValue);
    }

    /// <summary>
    /// Replaces the value after every key of <paramref name="key"/> whose <c>name</c> group is sensitive. The value
    /// is matched by <paramref name="value"/> (anchored with <c>\G</c>) from where the key ends; a key whose name is
    /// not sensitive is skipped from the end of its name, so its value is never scanned.
    /// </summary>
    private static string ReplaceSensitiveValues(string input, Regex key, Regex value, string replacement)
    {
        StringBuilder? builder = null;
        var copied = 0;
        var position = 0;

        while (position <= input.Length)
        {
            var keyMatch = key.Match(input, position);
            if (!keyMatch.Success)
            {
                break;
            }

            var name = keyMatch.Groups["name"];
            var nameEnd = name.Index + name.Length;
            if (!SensitiveFieldName.IsSensitive(name.Value))
            {
                position = nameEnd;
                continue;
            }

            var valueStart = keyMatch.Index + keyMatch.Length;
            var valueMatch = value.Match(input, valueStart);
            if (!valueMatch.Success)
            {
                position = nameEnd;
                continue;
            }

            builder ??= new StringBuilder(input.Length);
            builder.Append(input, copied, valueStart - copied).Append(replacement);
            copied = valueStart + valueMatch.Length;
            position = copied;
        }

        if (builder is null)
        {
            return input;
        }

        return builder.Append(input, copied, input.Length - copied).ToString();
    }
}
