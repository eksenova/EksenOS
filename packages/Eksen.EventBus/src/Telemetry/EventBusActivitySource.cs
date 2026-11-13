using System.Diagnostics;
using Eksen.Telemetry;
using Eksen.Telemetry.Propagation;

namespace Eksen.EventBus.Telemetry;

/// <summary>
/// The <see cref="ActivitySource"/> of the event bus and the helpers transports use to carry W3C trace context
/// across a message hop.
/// </summary>
public static class EventBusActivitySource
{
    public const string MessagingSystemName = "eksen";

    public static ActivitySource Instance { get; } = new(EksenTelemetry.EventBus);

    /// <summary>
    /// Returns a copy of <paramref name="headers"/> carrying the trace context of <paramref name="activity"/>.
    /// When <paramref name="activity"/> is <see langword="null"/> the headers are returned unchanged, so a context
    /// captured earlier (for example the one stored in an outbox row) is preserved.
    /// </summary>
    public static Dictionary<string, string>? WithTraceContext(
        Activity? activity,
        IReadOnlyDictionary<string, string>? headers)
    {
        if (activity is null)
        {
            return headers as Dictionary<string, string> ?? headers?.ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        var result = headers is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(headers, StringComparer.Ordinal);

        TraceContextPropagation.Inject(activity, result);
        return result;
    }

    /// <summary>
    /// Returns the headers to send: <paramref name="headers"/> unchanged when they already carry a
    /// <c>traceparent</c>, otherwise a copy with the trace context of <see cref="Activity.Current"/>.
    /// </summary>
    public static Dictionary<string, string>? WithCurrentTraceContextIfMissing(Dictionary<string, string>? headers)
    {
        if (headers is not null && headers.Keys.Any(key =>
                string.Equals(key, TraceContextPropagation.TraceParentHeader, StringComparison.OrdinalIgnoreCase)))
        {
            return headers;
        }

        return WithTraceContext(Activity.Current, headers);
    }

    /// <summary>
    /// Starts a consumer span parented on the remote context extracted from a message. Without a remote context the
    /// span is a new root rather than a child of whatever activity happens to be current on the listener loop.
    /// </summary>
    public static Activity? StartConsumer(
        ActivitySource source,
        string name,
        ActivityContext remoteParent,
        IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        return remoteParent == default
            ? EksenActivity.StartRoot(source, name, ActivityKind.Consumer, tags: tags)
            : source.StartActivity(name, ActivityKind.Consumer, remoteParent, tags);
    }
}
