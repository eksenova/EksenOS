using System.Collections;
using Eksen.Telemetry.Redaction;
using Microsoft.Extensions.Logging;

namespace Eksen.Telemetry.OpenTelemetry.Logging;

/// <summary>
/// Wraps the logger factory's scope provider so every scope a logger provider reads is redacted: structured scopes
/// (<see cref="IEnumerable{T}"/> of key/value pairs) get their values redacted like log attributes, and
/// string scopes get their sensitive query values and body fields redacted. <see cref="RedactedLogFields"/> scopes
/// were redacted when they were built and pass through as they are. Pushing is passed through unchanged.
/// </summary>
public sealed class RedactingExternalScopeProvider(IExternalScopeProvider inner) : IExternalScopeProvider
{
    public void ForEachScope<TState>(Action<object?, TState> callback, TState state)
    {
        ArgumentNullException.ThrowIfNull(callback);

        inner.ForEachScope(
            static (scope, context) => context.Callback(RedactScope(scope), context.State),
            (Callback: callback, State: state));
    }

    public IDisposable Push(object? state)
    {
        return inner.Push(state);
    }

    /// <summary>
    /// Returns the redacted scope, or <paramref name="scope"/> itself when it holds nothing to redact.
    /// </summary>
    public static object? RedactScope(object? scope)
    {
        switch (scope)
        {
            case null:
                return null;
            case RedactedLogFields:
                return scope;
            case string text:
                return LogValueRedaction.RedactText(text);
            case IEnumerable<KeyValuePair<string, object?>> pairs:
            {
                LogValueRedaction.RedactPairs(pairs, out var redacted, out var renderFromTemplate);
                if (redacted is null)
                {
                    var original = scope.ToString();
                    var redactedMessage = original is null ? null : LogValueRedaction.RedactText(original);
                    return ReferenceEquals(redactedMessage, original)
                        ? scope
                        : new RedactedScope(pairs.ToArray(), redactedMessage);
                }

                return new RedactedScope(
                    redacted,
                    LogValueRedaction.RedactMessage(scope.ToString(), redacted, renderFromTemplate));
            }
            default:
                return scope;
        }
    }

    /// <summary>
    /// A redacted copy of a structured scope. <see cref="ToString"/> returns the redacted scope message, which the
    /// console formatters write next to the values.
    /// </summary>
    private sealed class RedactedScope(IReadOnlyList<KeyValuePair<string, object?>> items, string? message)
        : IReadOnlyList<KeyValuePair<string, object?>>
    {
        public int Count
        {
            get { return items.Count; }
        }

        public KeyValuePair<string, object?> this[int index]
        {
            get { return items[index]; }
        }

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string? ToString()
        {
            return message;
        }
    }
}
