using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Eksen.Telemetry;

namespace Eksen.ErrorHandling.AspNetCore;

/// <summary>
/// The <c>Eksen.ErrorHandling</c> meter.
/// </summary>
internal sealed class ErrorHandlingTelemetry
{
    public const string ErrorKindTag = "eksen.error.kind";

    private static readonly ConditionalWeakTable<IMeterFactory, ErrorHandlingTelemetry> PerFactory = new();
    private static readonly Lazy<ErrorHandlingTelemetry> SharedInstance = new(() => new ErrorHandlingTelemetry(meterFactory: null));

    public ErrorHandlingTelemetry(IMeterFactory? meterFactory = null)
    {
        Meter = meterFactory?.Create(EksenTelemetry.ErrorHandling) ?? new Meter(EksenTelemetry.ErrorHandling);
        ErrorsHandled = Meter.CreateCounter<long>(
            "eksen.errors.handled",
            unit: "{error}",
            description: "Exceptions turned into an error response by the Eksen exception handler.");
    }

    public static ErrorHandlingTelemetry Shared
    {
        get { return SharedInstance.Value; }
    }

    public Meter Meter { get; }

    public Counter<long> ErrorsHandled { get; }

    public static ErrorHandlingTelemetry For(IMeterFactory? meterFactory)
    {
        return meterFactory is null
            ? Shared
            : PerFactory.GetValue(meterFactory, static factory => new ErrorHandlingTelemetry(factory));
    }
}
