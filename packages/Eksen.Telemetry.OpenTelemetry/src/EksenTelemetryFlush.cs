using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Eksen.Telemetry.OpenTelemetry;

/// <summary>
/// Flushes buffered spans, metrics and log records, for short-lived command line modes that exit before the
/// batch exporters' next tick.
/// </summary>
public static class EksenTelemetryFlush
{
    /// <returns><see langword="true"/> when every registered provider flushed within <paramref name="timeout"/>.</returns>
    public static Task<bool> ForceFlushAsync(IServiceProvider serviceProvider, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var tracerProvider = serviceProvider.GetService<TracerProvider>();
        var meterProvider = serviceProvider.GetService<MeterProvider>();
        var loggerProvider = serviceProvider.GetService<LoggerProvider>();

        if (tracerProvider is null && meterProvider is null && loggerProvider is null)
        {
            return Task.FromResult(true);
        }

        return Task.Run(() =>
        {
            var deadline = DateTime.UtcNow + timeout;
            var flushed = true;
            flushed &= tracerProvider?.ForceFlush(Remaining(deadline)) ?? true;
            flushed &= meterProvider?.ForceFlush(Remaining(deadline)) ?? true;
            flushed &= loggerProvider?.ForceFlush(Remaining(deadline)) ?? true;
            return flushed;
        });
    }

    private static int Remaining(DateTime deadline)
    {
        var remaining = deadline - DateTime.UtcNow;
        return (int)Math.Clamp(remaining.TotalMilliseconds, 0, int.MaxValue);
    }
}
