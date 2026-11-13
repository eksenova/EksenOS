using Eksen.Telemetry.Redaction;

namespace Eksen.Telemetry.OpenTelemetry.Logging;

/// <summary>
/// The redaction shared by the log record processor, the scope provider and the console formatter: values under a
/// sensitive name become <c>[redacted]</c>, string and <see cref="Uri"/> values have their sensitive query values
/// and body fields redacted, and a message rendered with a secret is rebuilt from its <c>{OriginalFormat}</c>.
/// </summary>
internal static class LogValueRedaction
{
    public const string OriginalFormatKey = "{OriginalFormat}";

    /// <summary>
    /// Returns the redacted value, or <paramref name="value"/> itself when nothing changed.
    /// </summary>
    public static object? RedactValue(string key, object? value)
    {
        if (value is null || key == OriginalFormatKey)
        {
            return value;
        }

        if (TelemetryValueRedactor.IsSensitiveName(key))
        {
            return TelemetryValueRedactor.RedactedValue;
        }

        var text = value switch
        {
            string stringValue => stringValue,
            Uri uri => uri.OriginalString,
            _ => null
        };

        if (text is null)
        {
            return value;
        }

        var redacted = TelemetryValueRedactor.RedactText(text);
        if (value is string && string.Equals(redacted, text, StringComparison.Ordinal))
        {
            return value;
        }

        return redacted;
    }

    /// <summary>
    /// Returns the redacted text, or <paramref name="value"/> itself when nothing changed.
    /// </summary>
    public static string RedactText(string value)
    {
        var redacted = TelemetryValueRedactor.RedactText(value);
        return string.Equals(redacted, value, StringComparison.Ordinal) ? value : redacted;
    }

    /// <summary>
    /// Redacts a list of structured values. <paramref name="redacted"/> is <see langword="null"/> when nothing
    /// changed; <paramref name="renderFromTemplate"/> is true when a sensitive-named value was present, so a
    /// message already rendered from these values carries the secret and must be rebuilt.
    /// </summary>
    public static void RedactPairs(
        IEnumerable<KeyValuePair<string, object?>> pairs,
        out List<KeyValuePair<string, object?>>? redacted,
        out bool renderFromTemplate)
    {
        redacted = null;
        renderFromTemplate = false;

        var items = pairs as IReadOnlyList<KeyValuePair<string, object?>> ?? pairs.ToArray();
        for (var index = 0; index < items.Count; index++)
        {
            var (key, value) = items[index];
            var redactedValue = RedactValue(key, value);
            if (value is not null && key != OriginalFormatKey && TelemetryValueRedactor.IsSensitiveName(key))
            {
                renderFromTemplate = true;
            }

            if (redacted is null && !ReferenceEquals(redactedValue, value))
            {
                redacted = new List<KeyValuePair<string, object?>>(items.Count);
                for (var previous = 0; previous < index; previous++)
                {
                    redacted.Add(items[previous]);
                }
            }

            redacted?.Add(new KeyValuePair<string, object?>(key, redactedValue));
        }
    }

    /// <summary>
    /// The message to write for structured values: rebuilt from the template with the redacted values when the
    /// original rendering carried a sensitive-named value, then passed through the text redaction.
    /// </summary>
    public static string? RedactMessage(
        string? message,
        IReadOnlyList<KeyValuePair<string, object?>>? redactedPairs,
        bool renderFromTemplate)
    {
        if (message is null)
        {
            return null;
        }

        if (renderFromTemplate && redactedPairs is not null && RedactingLogProcessor.TryRender(redactedPairs, out var rendered))
        {
            message = rendered;
        }

        return message.Length == 0 ? message : RedactText(message);
    }
}
