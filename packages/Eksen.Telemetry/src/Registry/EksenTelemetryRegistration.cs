using Microsoft.Extensions.DependencyInjection;

namespace Eksen.Telemetry.Registry;

/// <summary>
/// Declares an <see cref="System.Diagnostics.ActivitySource"/> name the OpenTelemetry wiring must listen to.
/// </summary>
public sealed record EksenTelemetrySourceRegistration(string Name);

/// <summary>
/// Declares a <see cref="System.Diagnostics.Metrics.Meter"/> name the OpenTelemetry wiring must collect.
/// </summary>
public sealed record EksenTelemetryMeterRegistration(string Name);

public static class EksenTelemetryRegistry
{
    public static IReadOnlyList<string> GetSourceNames(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetServices<EksenTelemetrySourceRegistration>()
            .Select(registration => registration.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<string> GetMeterNames(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetServices<EksenTelemetryMeterRegistration>()
            .Select(registration => registration.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
