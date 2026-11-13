using System.Diagnostics;
using System.Text;

namespace Eksen.Telemetry.Propagation;

/// <summary>
/// Writes and reads W3C trace context (<c>traceparent</c>, <c>tracestate</c>, <c>baggage</c>) to and from
/// message headers through <see cref="DistributedContextPropagator.Current"/>.
/// </summary>
public static class TraceContextPropagation
{
    public const string TraceParentHeader = "traceparent";
    public const string TraceStateHeader = "tracestate";
    public const string BaggageHeader = "baggage";

    private static readonly string[] TraceHeaders =
    [
        TraceParentHeader,
        TraceStateHeader,
        BaggageHeader,
        "Request-Id",
        "Correlation-Context"
    ];

    /// <summary>
    /// Writes the trace context of <paramref name="activity"/> into <paramref name="headers"/>, replacing any
    /// trace keys already present. A <see langword="null"/> activity only removes the stale keys.
    /// </summary>
    public static void Inject(Activity? activity, IDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        foreach (var existingKey in headers.Keys.ToArray())
        {
            if (IsTraceHeader(existingKey))
            {
                headers.Remove(existingKey);
            }
        }

        if (activity is null)
        {
            return;
        }

        DistributedContextPropagator.Current.Inject(activity, headers, static (carrier, name, value) =>
        {
            if (carrier is IDictionary<string, string> target && value is not null)
            {
                target[name] = value;
            }
        });
    }

    /// <summary>
    /// Reads the remote parent context from string headers. Returns <see langword="default"/> when the headers
    /// are absent or the <c>traceparent</c> is invalid.
    /// </summary>
    public static ActivityContext Extract(
        IReadOnlyDictionary<string, string>? headers,
        out IEnumerable<KeyValuePair<string, string?>> baggage)
    {
        baggage = [];
        if (headers is null || headers.Count == 0)
        {
            return default;
        }

        return ExtractCore(headers, static (carrier, name) =>
        {
            var source = (IReadOnlyDictionary<string, string>)carrier!;
            return TryGetIgnoreCase(source, name, out var value) ? value : null;
        }, out baggage);
    }

    /// <summary>
    /// Reads the remote parent context from AMQP headers, whose values arrive as UTF-8 <see cref="byte"/> arrays
    /// (or as strings when produced in process).
    /// </summary>
    public static ActivityContext ExtractFromAmqp(
        IDictionary<string, object?>? headers,
        out IEnumerable<KeyValuePair<string, string?>> baggage)
    {
        baggage = [];
        if (headers is null || headers.Count == 0)
        {
            return default;
        }

        return ExtractCore(headers, static (carrier, name) =>
        {
            var source = (IDictionary<string, object?>)carrier!;
            foreach (var (key, value) in source)
            {
                if (!string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return value switch
                {
                    byte[] bytes => Encoding.UTF8.GetString(bytes),
                    ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
                    string text => text,
                    _ => null
                };
            }

            return null;
        }, out baggage);
    }

    private static ActivityContext ExtractCore(
        object carrier,
        Func<object?, string, string?> read,
        out IEnumerable<KeyValuePair<string, string?>> baggage)
    {
        var propagator = DistributedContextPropagator.Current;

        propagator.ExtractTraceIdAndState(
            carrier,
            (state, name, out value, out values) =>
            {
                values = null;
                value = read(state, name);
            },
            out var traceParent,
            out var traceState);

        baggage = propagator.ExtractBaggage(
            carrier,
            (state, name, out value, out values) =>
            {
                values = null;
                value = read(state, name);
            }) ?? [];

        if (string.IsNullOrWhiteSpace(traceParent))
        {
            return default;
        }

        return ActivityContext.TryParse(traceParent, traceState, isRemote: true, out var context)
            ? context
            : default;
    }

    private static bool IsTraceHeader(string key)
    {
        foreach (var header in TraceHeaders)
        {
            if (string.Equals(header, key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetIgnoreCase(IReadOnlyDictionary<string, string> source, string name, out string? value)
    {
        if (source.TryGetValue(name, out var exact))
        {
            value = exact;
            return true;
        }

        foreach (var (key, candidate) in source)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = candidate;
                return true;
            }
        }

        value = null;
        return false;
    }
}
