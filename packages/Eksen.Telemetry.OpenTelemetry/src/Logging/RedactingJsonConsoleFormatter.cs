using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Eksen.Telemetry.OpenTelemetry.Logging;

/// <summary>
/// The JSON console format of <c>AddJsonConsole</c> (same fields: <c>Timestamp</c>, <c>EventId</c>,
/// <c>LogLevel</c>, <c>Category</c>, <c>Message</c>, <c>Exception</c>, <c>State</c>, <c>Scopes</c>), with the
/// redaction applied to exported logs: state and scope values under sensitive names become <c>[redacted]</c>,
/// messages are rebuilt from their template when they were rendered with such a value, and every text passes the
/// URL and body redactors. Container stdout therefore never carries what OTLP export redacts.
/// </summary>
public sealed class RedactingJsonConsoleFormatter : ConsoleFormatter, IDisposable
{
    public const string FormatterName = "eksen-json";

    private readonly IDisposable? _optionsReloadToken;

    public RedactingJsonConsoleFormatter(IOptionsMonitor<JsonConsoleFormatterOptions> options)
        : base(FormatterName)
    {
        FormatterOptions = options.CurrentValue;
        _optionsReloadToken = options.OnChange(updated => FormatterOptions = updated);
    }

    internal JsonConsoleFormatterOptions FormatterOptions { get; set; }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (logEntry.Exception is null && message is null)
        {
            return;
        }

        var options = FormatterOptions;

        IReadOnlyList<KeyValuePair<string, object?>>? stateValues = null;
        var renderFromTemplate = false;
        if (logEntry.State is IEnumerable<KeyValuePair<string, object?>> statePairs)
        {
            LogValueRedaction.RedactPairs(statePairs, out var redacted, out renderFromTemplate);
            stateValues = redacted ?? statePairs as IReadOnlyList<KeyValuePair<string, object?>> ?? statePairs.ToArray();
        }

        message = LogValueRedaction.RedactMessage(message, stateValues, renderFromTemplate);

        var output = new ArrayBufferWriter<byte>(1024);
        using (var writer = new Utf8JsonWriter(output, options.JsonWriterOptions))
        {
            writer.WriteStartObject();

            if (options.TimestampFormat is { } timestampFormat)
            {
                var now = options.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
                writer.WriteString("Timestamp", now.ToString(timestampFormat, CultureInfo.InvariantCulture));
            }

            writer.WriteNumber(nameof(logEntry.EventId), logEntry.EventId.Id);
            writer.WriteString(nameof(logEntry.LogLevel), GetLogLevelString(logEntry.LogLevel));
            writer.WriteString(nameof(logEntry.Category), logEntry.Category);
            writer.WriteString("Message", message);

            if (logEntry.Exception is { } exception)
            {
                writer.WriteString(nameof(logEntry.Exception), LogValueRedaction.RedactText(exception.ToString()));
            }

            if (logEntry.State is not null)
            {
                writer.WriteStartObject(nameof(logEntry.State));
                writer.WriteString("Message", stateValues is not null
                    ? message
                    : LogValueRedaction.RedactText(logEntry.State.ToString() ?? string.Empty));

                if (stateValues is not null)
                {
                    foreach (var item in stateValues)
                    {
                        WriteItem(writer, item);
                    }
                }

                writer.WriteEndObject();
            }

            WriteScopes(writer, scopeProvider, options);

            writer.WriteEndObject();
        }

        textWriter.Write(Encoding.UTF8.GetString(output.WrittenSpan));
        textWriter.Write(Environment.NewLine);
    }

    public void Dispose()
    {
        _optionsReloadToken?.Dispose();
    }

    private static void WriteScopes(Utf8JsonWriter writer, IExternalScopeProvider? scopeProvider, JsonConsoleFormatterOptions options)
    {
        if (!options.IncludeScopes || scopeProvider is null)
        {
            return;
        }

        writer.WriteStartArray("Scopes");
        new RedactingExternalScopeProvider(scopeProvider).ForEachScope(static (scope, state) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object?>> scopeItems)
            {
                state.WriteStartObject();
                state.WriteString("Message", scope.ToString());
                foreach (var item in scopeItems)
                {
                    WriteItem(state, item);
                }

                state.WriteEndObject();
            }
            else
            {
                state.WriteStringValue(ToInvariantString(scope));
            }
        }, writer);
        writer.WriteEndArray();
    }

    private static void WriteItem(Utf8JsonWriter writer, KeyValuePair<string, object?> item)
    {
        var key = item.Key;
        switch (item.Value)
        {
            case bool boolValue:
                writer.WriteBoolean(key, boolValue);
                break;
            case byte byteValue:
                writer.WriteNumber(key, byteValue);
                break;
            case sbyte sbyteValue:
                writer.WriteNumber(key, sbyteValue);
                break;
            case char charValue:
                writer.WriteString(key, charValue.ToString());
                break;
            case decimal decimalValue:
                writer.WriteNumber(key, decimalValue);
                break;
            case double doubleValue when double.IsFinite(doubleValue):
                writer.WriteNumber(key, doubleValue);
                break;
            case float floatValue when float.IsFinite(floatValue):
                writer.WriteNumber(key, floatValue);
                break;
            case int intValue:
                writer.WriteNumber(key, intValue);
                break;
            case uint uintValue:
                writer.WriteNumber(key, uintValue);
                break;
            case long longValue:
                writer.WriteNumber(key, longValue);
                break;
            case ulong ulongValue:
                writer.WriteNumber(key, ulongValue);
                break;
            case short shortValue:
                writer.WriteNumber(key, shortValue);
                break;
            case ushort ushortValue:
                writer.WriteNumber(key, ushortValue);
                break;
            case null:
                writer.WriteNull(key);
                break;
            default:
                writer.WriteString(key, ToInvariantString(item.Value));
                break;
        }
    }

    private static string? ToInvariantString(object? value)
    {
        return value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value?.ToString();
    }

    private static string GetLogLevelString(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "Trace",
            LogLevel.Debug => "Debug",
            LogLevel.Information => "Information",
            LogLevel.Warning => "Warning",
            LogLevel.Error => "Error",
            LogLevel.Critical => "Critical",
            _ => logLevel.ToString()
        };
    }
}
