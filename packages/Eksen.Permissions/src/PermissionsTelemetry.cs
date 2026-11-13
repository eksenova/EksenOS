using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Eksen.Telemetry;

namespace Eksen.Permissions;

/// <summary>
/// The <c>Eksen.Permissions</c> activity source and meter. The container registers one instance whose meter comes
/// from its <see cref="IMeterFactory"/>; caches and checkers built without a factory share a process-wide meter.
/// </summary>
internal sealed class PermissionsTelemetry
{
    public const string CacheResultTag = "cache.result";
    public const string KeyKindTag = "key_kind";
    public const string DefinitionsKeyKind = "definitions";
    public const string UserKeyKind = "user";

    private static readonly ConditionalWeakTable<IMeterFactory, PermissionsTelemetry> PerFactory = new();
    private static readonly Lazy<PermissionsTelemetry> SharedInstance = new(() => new PermissionsTelemetry(meterFactory: null));

    public PermissionsTelemetry(IMeterFactory? meterFactory = null)
    {
        Meter = meterFactory?.Create(EksenTelemetry.Permissions) ?? new Meter(EksenTelemetry.Permissions);
        CacheRequests = Meter.CreateCounter<long>(
            "eksen.permissions.cache.requests",
            unit: "{request}",
            description: "Permission cache lookups, by hit or miss and key kind.");
        CheckDuration = Meter.CreateHistogram<double>(
            "eksen.permissions.check.duration",
            unit: "s",
            description: "Duration of a permission check.");
        CacheInvalidations = Meter.CreateCounter<long>(
            "eksen.permissions.cache.invalidations",
            unit: "{invalidation}",
            description: "Permission cache invalidations, by key kind.");
    }

    public static ActivitySource ActivitySource { get; } = new(EksenTelemetry.Permissions);

    public static PermissionsTelemetry Shared
    {
        get { return SharedInstance.Value; }
    }

    public Meter Meter { get; }

    public static PermissionsTelemetry For(IMeterFactory? meterFactory)
    {
        return meterFactory is null
            ? Shared
            : PerFactory.GetValue(meterFactory, static factory => new PermissionsTelemetry(factory));
    }

    public Counter<long> CacheRequests { get; }

    public Histogram<double> CheckDuration { get; }

    public Counter<long> CacheInvalidations { get; }

    public void RecordCacheRequest(bool hit, string keyKind)
    {
        CacheRequests.Add(
            1,
            new KeyValuePair<string, object?>(CacheResultTag, hit ? "hit" : "miss"),
            new KeyValuePair<string, object?>(KeyKindTag, keyKind));
    }

    public void RecordInvalidation(string keyKind, long count = 1)
    {
        if (count > 0)
        {
            CacheInvalidations.Add(count, new KeyValuePair<string, object?>(KeyKindTag, keyKind));
        }
    }

    public Activity? StartStoreLoad(string keyKind)
    {
        return ActivitySource.StartActivity(
            "permissions load",
            ActivityKind.Internal,
            parentContext: default,
            tags: [new KeyValuePair<string, object?>(KeyKindTag, keyKind)]);
    }
}
