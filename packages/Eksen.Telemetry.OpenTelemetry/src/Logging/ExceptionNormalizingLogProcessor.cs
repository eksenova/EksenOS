using Eksen.Telemetry.Redaction;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Eksen.Telemetry.OpenTelemetry.Logging;

/// <summary>
/// Moves <see cref="LogRecord.Exception"/> into the <c>exception.type</c>, <c>exception.message</c> and
/// <c>exception.stacktrace</c> attributes (built by <see cref="ExceptionFormatter"/>, so the stack trace does
/// not repeat the message) and clears the exception so the exporter does not emit it a second time. The message
/// and stack trace pass through <see cref="TelemetryValueRedactor.RedactText"/>, because this processor runs
/// after <see cref="RedactingLogProcessor"/>.
/// </summary>
public sealed class ExceptionNormalizingLogProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord data)
    {
        var exception = data.Exception;
        if (exception is null)
        {
            return;
        }

        var attributes = new List<KeyValuePair<string, object?>>((data.Attributes?.Count ?? 0) + 3);
        if (data.Attributes is not null)
        {
            foreach (var attribute in data.Attributes)
            {
                if (attribute.Key is EksenSemanticConventions.ExceptionType
                    or EksenSemanticConventions.ExceptionMessage
                    or EksenSemanticConventions.ExceptionStacktrace)
                {
                    continue;
                }

                attributes.Add(attribute);
            }
        }

        var formatted = ExceptionFormatter.Format(exception);
        formatted = formatted with
        {
            Message = formatted.Message is null ? null : TelemetryValueRedactor.RedactText(formatted.Message),
            StackTrace = formatted.StackTrace is null ? null : TelemetryValueRedactor.RedactText(formatted.StackTrace)
        };

        attributes.AddRange(formatted.ToAttributes());

        data.Attributes = attributes;
        data.Exception = null;
    }
}
