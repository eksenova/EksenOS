using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Eksen.Telemetry.OpenTelemetry.Tests;

public sealed record CapturedLogRecord(
    string? CategoryName,
    string? Body,
    string? FormattedMessage,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Attributes,
    IReadOnlyDictionary<string, object?>? Scopes = null);

/// <summary>
/// Snapshots log records as the exporter would see them (log records are pooled, so they are copied here).
/// </summary>
public sealed class CapturingLogProcessor : BaseProcessor<LogRecord>
{
    private readonly List<CapturedLogRecord> _records = [];

    public IReadOnlyList<CapturedLogRecord> Records
    {
        get
        {
            lock (_records)
            {
                return _records.ToArray();
            }
        }
    }

    public override void OnEnd(LogRecord data)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (data.Attributes is not null)
        {
            foreach (var (key, value) in data.Attributes)
            {
                attributes[key] = value;
            }
        }

        var scopes = new Dictionary<string, object?>(StringComparer.Ordinal);
        data.ForEachScope(static (scope, target) =>
        {
            foreach (var (key, value) in scope)
            {
                target[key] = value;
            }
        }, scopes);

        lock (_records)
        {
            _records.Add(new CapturedLogRecord(data.CategoryName, data.Body, data.FormattedMessage, data.Exception, attributes, scopes));
        }
    }
}
