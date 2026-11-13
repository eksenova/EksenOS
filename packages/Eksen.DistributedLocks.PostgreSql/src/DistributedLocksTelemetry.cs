using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Eksen.Telemetry;

namespace Eksen.DistributedLocks.PostgreSql;

/// <summary>
/// The <c>Eksen.DistributedLocks</c> meter.
/// </summary>
internal sealed class DistributedLocksTelemetry
{
    public const string LockNameTag = "eksen.lock.name";
    public const string ResultTag = "result";
    public const string AnonymousLockName = "anonymous";

    private static readonly ConditionalWeakTable<IMeterFactory, DistributedLocksTelemetry> PerFactory = new();
    private static readonly Lazy<DistributedLocksTelemetry> SharedInstance = new(() => new DistributedLocksTelemetry(meterFactory: null));

    public DistributedLocksTelemetry(IMeterFactory? meterFactory = null)
    {
        Meter = meterFactory?.Create(EksenTelemetry.DistributedLocks) ?? new Meter(EksenTelemetry.DistributedLocks);
        WaitDuration = Meter.CreateHistogram<double>(
            "eksen.lock.wait.duration",
            unit: "s",
            description: "Time spent waiting for a distributed lock, by result.");
        Acquisitions = Meter.CreateCounter<long>(
            "eksen.lock.acquisitions",
            unit: "{acquisition}",
            description: "Distributed lock acquisition attempts, by result.");
        HoldDuration = Meter.CreateHistogram<double>(
            "eksen.lock.hold.duration",
            unit: "s",
            description: "Time a distributed lock was held before release.");
    }

    public static DistributedLocksTelemetry Shared
    {
        get { return SharedInstance.Value; }
    }

    public Meter Meter { get; }

    public Histogram<double> WaitDuration { get; }

    public Counter<long> Acquisitions { get; }

    public Histogram<double> HoldDuration { get; }

    public static DistributedLocksTelemetry For(IMeterFactory? meterFactory)
    {
        return meterFactory is null
            ? Shared
            : PerFactory.GetValue(meterFactory, static factory => new DistributedLocksTelemetry(factory));
    }

    public void RecordAcquisition(string lockName, string result, long waitStartedAt)
    {
        var tags = new TagList
        {
            { LockNameTag, lockName },
            { ResultTag, result }
        };

        Acquisitions.Add(1, tags);
        WaitDuration.Record(Stopwatch.GetElapsedTime(waitStartedAt).TotalSeconds, tags);
    }

    public void RecordHold(string lockName, long acquiredAt)
    {
        HoldDuration.Record(
            Stopwatch.GetElapsedTime(acquiredAt).TotalSeconds,
            new KeyValuePair<string, object?>(LockNameTag, lockName));
    }
}
