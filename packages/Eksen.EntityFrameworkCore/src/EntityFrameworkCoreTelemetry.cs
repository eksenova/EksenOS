using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Eksen.Telemetry;

namespace Eksen.EntityFrameworkCore;

/// <summary>
/// The <c>Eksen.EntityFrameworkCore</c> meter.
/// </summary>
internal sealed class EntityFrameworkCoreTelemetry
{
    public const string CommandDurationName = "eksen.db.command.duration";

    private static readonly ConditionalWeakTable<IMeterFactory, EntityFrameworkCoreTelemetry> PerFactory = new();
    private static readonly Lazy<EntityFrameworkCoreTelemetry> SharedInstance = new(() => new EntityFrameworkCoreTelemetry(meterFactory: null));

    public EntityFrameworkCoreTelemetry(IMeterFactory? meterFactory = null)
    {
        Meter = meterFactory?.Create(EksenTelemetry.EntityFrameworkCore) ?? new Meter(EksenTelemetry.EntityFrameworkCore);
        CommandDuration = Meter.CreateHistogram<double>(
            CommandDurationName,
            unit: "s",
            description: "Duration of database commands executed through Entity Framework Core.");
    }

    public static EntityFrameworkCoreTelemetry Shared
    {
        get { return SharedInstance.Value; }
    }

    public Meter Meter { get; }

    public Histogram<double> CommandDuration { get; }

    public static EntityFrameworkCoreTelemetry For(IMeterFactory? meterFactory)
    {
        return meterFactory is null
            ? Shared
            : PerFactory.GetValue(meterFactory, static factory => new EntityFrameworkCoreTelemetry(factory));
    }
}
