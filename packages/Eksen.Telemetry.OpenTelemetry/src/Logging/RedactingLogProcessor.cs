using Eksen.Telemetry.Redaction;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Eksen.Telemetry.OpenTelemetry.Logging;

/// <summary>
/// Redacts every log record before export: string attributes, the body and the formatted message pass through
/// <see cref="SensitiveUrlRedactor"/> and then <see cref="SensitiveBodyRedactor"/>; attributes whose name is
/// sensitive (see <see cref="SensitiveFieldName"/>) are replaced with <c>[redacted]</c>. Scopes cannot be changed
/// by a processor; <see cref="ScopeRedactingLoggerProvider"/> redacts them.
/// </summary>
public sealed class RedactingLogProcessor : BaseProcessor<LogRecord>
{
    private const string OriginalFormatKey = LogValueRedaction.OriginalFormatKey;

    public override void OnEnd(LogRecord data)
    {
        var renderFromTemplate = false;

        if (data.Attributes is { Count: > 0 } attributes)
        {
            LogValueRedaction.RedactPairs(attributes, out var rewritten, out renderFromTemplate);
            if (rewritten is not null)
            {
                data.Attributes = rewritten;
            }
        }

        if (data.Body is { Length: > 0 } body)
        {
            data.Body = LogValueRedaction.RedactText(body);
        }

        if (data.FormattedMessage is { Length: > 0 } message)
        {
            data.FormattedMessage = LogValueRedaction.RedactMessage(message, data.Attributes, renderFromTemplate);
        }
    }

    /// <summary>
    /// Renders the <c>{OriginalFormat}</c> template with the (already redacted) attribute values.
    /// </summary>
    internal static bool TryRender(IReadOnlyList<KeyValuePair<string, object?>> attributes, out string rendered)
    {
        rendered = string.Empty;

        string? template = null;
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in attributes)
        {
            if (key == OriginalFormatKey)
            {
                template = value as string;
            }
            else
            {
                values.TryAdd(key, value);
            }
        }

        if (template is null)
        {
            return false;
        }

        var builder = new System.Text.StringBuilder(template.Length + 32);
        var index = 0;
        while (index < template.Length)
        {
            var open = template.IndexOf('{', index);
            if (open < 0)
            {
                builder.Append(template, index, template.Length - index);
                break;
            }

            if (open + 1 < template.Length && template[open + 1] == '{')
            {
                builder.Append(template, index, open - index + 1);
                index = open + 2;
                continue;
            }

            var close = template.IndexOf('}', open + 1);
            if (close < 0)
            {
                builder.Append(template, index, template.Length - index);
                break;
            }

            builder.Append(template, index, open - index);

            var hole = template.Substring(open + 1, close - open - 1);
            var formatStart = hole.IndexOfAny([',', ':']);
            var name = (formatStart >= 0 ? hole[..formatStart] : hole).TrimStart('@', '$');
            var format = formatStart >= 0 && hole[formatStart] == ':' ? hole[(formatStart + 1)..] : null;

            if (values.TryGetValue(name, out var value))
            {
                builder.Append(value switch
                {
                    null => "(null)",
                    IFormattable formattable => formattable.ToString(format, System.Globalization.CultureInfo.InvariantCulture),
                    _ => value.ToString()
                });
            }
            else
            {
                builder.Append(template, open, close - open + 1);
            }

            index = close + 1;
        }

        rendered = builder.ToString().Replace("}}", "}", StringComparison.Ordinal);
        return true;
    }
}
