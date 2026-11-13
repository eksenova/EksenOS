using Microsoft.Extensions.Logging;

namespace Eksen.Telemetry;

/// <summary>
/// Telemetry settings, bound from the <c>Telemetry</c> configuration section. When the section
/// leaves a value empty, the standard <c>OTEL_*</c> environment variables are honoured
/// (see <see cref="EksenTelemetryOptionsResolver"/>).
/// </summary>
public sealed class EksenTelemetryOptions
{
    public const string SectionName = "Telemetry";
    public const string GrpcProtocol = "grpc";
    public const string HttpProtobufProtocol = "http/protobuf";

    public static readonly IReadOnlyList<string> DefaultExcludedPaths =
    [
        "/healthz",
        "/openapi",
        "/scalar",
        "/favicon.ico"
    ];

    public bool Enabled { get; set; }

    public string? ServiceName { get; set; }

    public string? ServiceNamespace { get; set; }

    public string? ServiceRole { get; set; }

    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// <c>grpc</c> (default) or <c>http/protobuf</c>. <c>http</c> is accepted as an alias of <c>http/protobuf</c>.
    /// </summary>
    public string OtlpProtocol { get; set; } = GrpcProtocol;

    /// <summary>
    /// Exporter headers in the <c>OTEL_EXPORTER_OTLP_HEADERS</c> format: <c>key1=value1,key2=value2</c>.
    /// </summary>
    public string? OtlpHeaders { get; set; }

    public double SamplingRatio { get; set; } = 1.0;

    public bool RecordExceptions { get; set; } = true;

    public bool CaptureRequestBodies { get; set; }

    public int MaxRequestBodyBytes { get; set; } = 32 * 1024;

    public bool CaptureOutboundBodies { get; set; }

    public string[] RedactedHeaders { get; set; } = [];

    public string[] ExcludedPaths { get; set; } = DefaultExcludedPaths.ToArray();

    /// <summary>
    /// Write JSON lines to the console. Defaults to <see langword="true"/> outside Development.
    /// </summary>
    public bool? ConsoleJson { get; set; }

    public LogLevel ConsoleMinimumLevel { get; set; } = LogLevel.Warning;

    public bool CaptureDbStatements { get; set; } = true;

    public bool UnitOfWorkSpans { get; set; }

    /// <summary>
    /// Trace client spans (database, cache, HTTP) that start with no parent span. Off by default: such a span comes from
    /// work outside any request, message or job span, typically a background poll, and would otherwise be a one-span
    /// trace on every poll.
    /// </summary>
    public bool TraceOrphanClientSpans { get; set; }

    /// <summary>
    /// Kestrel port that serves the health endpoints only. Never exposed by a Service.
    /// </summary>
    public int? ManagementPort { get; set; }

    /// <summary>
    /// Additional <see cref="System.Diagnostics.ActivitySource"/> names to listen to.
    /// </summary>
    public string[] AdditionalSources { get; set; } = [];

    /// <summary>
    /// Additional <see cref="System.Diagnostics.Metrics.Meter"/> names to collect.
    /// </summary>
    public string[] AdditionalMeters { get; set; } = [];

    public bool IsHttpProtobuf
    {
        get
        {
            return string.Equals(OtlpProtocol, HttpProtobufProtocol, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(OtlpProtocol, "http", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// True when telemetry is switched on and has somewhere to send data.
    /// </summary>
    public bool IsExportEnabled
    {
        get { return Enabled && !string.IsNullOrWhiteSpace(OtlpEndpoint); }
    }

    public bool IsExcludedPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        foreach (var excluded in ExcludedPaths)
        {
            if (!string.IsNullOrEmpty(excluded) && path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public IReadOnlyList<KeyValuePair<string, string>> ParseOtlpHeaders()
    {
        return ParseKeyValueList(OtlpHeaders);
    }

    internal static IReadOnlyList<KeyValuePair<string, string>> ParseKeyValueList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var result = new List<KeyValuePair<string, string>>();
        foreach (var pair in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..separator].Trim());
            var entryValue = Uri.UnescapeDataString(pair[(separator + 1)..].Trim());
            result.Add(new KeyValuePair<string, string>(key, entryValue));
        }

        return result;
    }
}
