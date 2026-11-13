using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Eksen.Telemetry;

/// <summary>
/// Binds <see cref="EksenTelemetryOptions"/> from a configuration section and fills empty values from the
/// standard OpenTelemetry environment variables.
/// </summary>
public static class EksenTelemetryOptionsResolver
{
    public const string OtlpEndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";
    public const string OtlpHeadersVariable = "OTEL_EXPORTER_OTLP_HEADERS";
    public const string OtlpProtocolVariable = "OTEL_EXPORTER_OTLP_PROTOCOL";
    public const string ServiceNameVariable = "OTEL_SERVICE_NAME";
    public const string ResourceAttributesVariable = "OTEL_RESOURCE_ATTRIBUTES";

    public static EksenTelemetryOptions Resolve(
        IConfiguration telemetrySection,
        IHostEnvironment? environment = null,
        Func<string, string?>? getEnvironmentVariable = null)
    {
        var options = new EksenTelemetryOptions();
        Apply(options, telemetrySection, environment, getEnvironmentVariable);
        return options;
    }

    public static void Apply(
        EksenTelemetryOptions options,
        IConfiguration telemetrySection,
        IHostEnvironment? environment = null,
        Func<string, string?>? getEnvironmentVariable = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;

        // Arrays are appended to by the binder, so a configured list replaces the defaults explicitly.
        if (telemetrySection.GetSection(nameof(EksenTelemetryOptions.ExcludedPaths)).GetChildren().Any())
        {
            options.ExcludedPaths = [];
        }

        telemetrySection.Bind(options);

        if (string.IsNullOrWhiteSpace(options.OtlpEndpoint))
        {
            options.OtlpEndpoint = NullIfWhiteSpace(getEnvironmentVariable(OtlpEndpointVariable));
        }

        if (string.IsNullOrWhiteSpace(options.OtlpHeaders))
        {
            options.OtlpHeaders = NullIfWhiteSpace(getEnvironmentVariable(OtlpHeadersVariable));
        }

        if (string.IsNullOrWhiteSpace(telemetrySection[nameof(EksenTelemetryOptions.OtlpProtocol)]))
        {
            var protocol = NullIfWhiteSpace(getEnvironmentVariable(OtlpProtocolVariable));
            if (protocol is not null)
            {
                options.OtlpProtocol = protocol;
            }
        }

        if (string.IsNullOrWhiteSpace(options.ServiceName))
        {
            options.ServiceName = NullIfWhiteSpace(getEnvironmentVariable(ServiceNameVariable))
                                  ?? FindResourceAttribute(getEnvironmentVariable, "service.name")
                                  ?? NullIfWhiteSpace(environment?.ApplicationName)
                                  ?? "unknown_service";
        }

        if (string.IsNullOrWhiteSpace(options.ServiceNamespace))
        {
            options.ServiceNamespace = FindResourceAttribute(getEnvironmentVariable, "service.namespace");
        }

        options.ConsoleJson ??= environment is null || !environment.IsDevelopment();

        if (options.SamplingRatio is < 0 or > 1 || double.IsNaN(options.SamplingRatio))
        {
            throw new InvalidOperationException(
                $"Telemetry:SamplingRatio must be between 0 and 1, got {options.SamplingRatio}.");
        }
    }

    private static string? FindResourceAttribute(Func<string, string?> getEnvironmentVariable, string key)
    {
        foreach (var (name, value) in EksenTelemetryOptions.ParseKeyValueList(getEnvironmentVariable(ResourceAttributesVariable)))
        {
            if (string.Equals(name, key, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
