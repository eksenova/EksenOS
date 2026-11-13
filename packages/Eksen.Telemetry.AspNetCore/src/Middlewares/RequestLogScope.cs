using Eksen.Telemetry.AspNetCore.Enrichment;
using Eksen.Telemetry.Redaction;
using Microsoft.Extensions.Logging;

namespace Eksen.Telemetry.AspNetCore.Middlewares;

internal static class RequestLogScope
{
    public static IDisposable? Begin(ILogger logger, IEnumerable<RequestEnrichmentProperty> properties)
    {
        var fields = ToFields(properties);
        return fields.Count == 0 ? null : logger.BeginScope(fields);
    }

    /// <summary>
    /// The scope fields, redacted once here: <see cref="RedactedLogFields"/> tells the redacting scope provider not to
    /// redact them again for every record written inside the request.
    /// </summary>
    public static RedactedLogFields ToFields(IEnumerable<RequestEnrichmentProperty> properties)
    {
        var fields = new RedactedLogFields();
        foreach (var property in properties)
        {
            if (!RequestEnrichmentValues.IsEmpty(property.Value))
            {
                fields.Set(property.Name, property.Value);
            }
        }

        return fields;
    }

    /// <summary>
    /// Passes the value through <see cref="TelemetryValueRedactor.RedactValue"/>, so a field whose name is sensitive,
    /// or a value carrying a secret query value or body field, never reaches a log scope or event in clear text.
    /// </summary>
    public static object? Redact(string name, object? value)
    {
        return TelemetryValueRedactor.RedactValue(name, value);
    }
}
