using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Eksen.Telemetry;

namespace Eksen.UnitOfWork;

/// <summary>
/// The <c>Eksen.UnitOfWork</c> activity source and meter.
/// </summary>
internal sealed class UnitOfWorkTelemetry
{
    public const string CommitOutcome = "commit";
    public const string RollbackOutcome = "rollback";
    public const string DisposedUncommittedOutcome = "disposed_uncommitted";

    private static readonly ConditionalWeakTable<IMeterFactory, UnitOfWorkTelemetry> PerFactory = new();
    private static readonly Lazy<UnitOfWorkTelemetry> SharedInstance = new(() => new UnitOfWorkTelemetry(meterFactory: null));

    public UnitOfWorkTelemetry(IMeterFactory? meterFactory = null)
    {
        Meter = meterFactory?.Create(EksenTelemetry.UnitOfWork) ?? new Meter(EksenTelemetry.UnitOfWork);
        Duration = Meter.CreateHistogram<double>(
            "eksen.unit_of_work.duration",
            unit: "s",
            description: "Lifetime of a unit of work scope, by how it ended.");
        PostCommitFailures = Meter.CreateCounter<long>(
            "eksen.unit_of_work.post_commit_failures",
            unit: "{failure}",
            description: "Post-commit actions that threw after the unit of work had committed.");
    }

    public static ActivitySource ActivitySource { get; } = new(EksenTelemetry.UnitOfWork);

    public static UnitOfWorkTelemetry Shared
    {
        get { return SharedInstance.Value; }
    }

    public Meter Meter { get; }

    public Histogram<double> Duration { get; }

    public Counter<long> PostCommitFailures { get; }

    public static UnitOfWorkTelemetry For(IMeterFactory? meterFactory)
    {
        return meterFactory is null
            ? Shared
            : PerFactory.GetValue(meterFactory, static factory => new UnitOfWorkTelemetry(factory));
    }
}
