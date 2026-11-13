using System.Diagnostics;
using Eksen.Telemetry.Redaction;

namespace Eksen.Telemetry;

public static class EksenActivity
{
    /// <summary>
    /// Starts an activity that has no parent, even when an ambient <see cref="Activity.Current"/> exists.
    /// Use it for background work that must not be attached to whatever span happens to be current.
    /// When the returned activity is disposed, <see cref="Activity.Current"/> becomes <see langword="null"/>.
    /// </summary>
    public static Activity? StartRoot(
        ActivitySource source,
        string name,
        ActivityKind kind,
        IEnumerable<ActivityLink>? links = null,
        IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var previous = Activity.Current;
        Activity.Current = null;

        var activity = source.StartActivity(name, kind, parentContext: default, tags, links);
        if (activity is null)
        {
            Activity.Current = previous;
        }

        return activity;
    }

    /// <summary>
    /// Records <paramref name="exception"/> as a span event, marks the activity as failed and sets
    /// <c>error.type</c> to the exception's full type name. The message and stack trace pass through
    /// <see cref="TelemetryValueRedactor.RedactText"/>, so query tokens, secret body fields or e-mail addresses
    /// quoted in an exception message do not reach the span.
    /// </summary>
    public static void RecordException(this Activity? activity, Exception exception)
    {
        RecordException(activity, exception, markFailed: true);
    }

    /// <summary>
    /// Records <paramref name="exception"/> as a redacted span event. With <paramref name="markFailed"/> set it also
    /// marks the activity as failed and sets <c>error.type</c> to the exception's full type name; without it the span
    /// status and <c>error.type</c> stay the caller's choice (an error handler that maps the exception to its own
    /// error code, for example).
    /// </summary>
    public static void RecordException(this Activity? activity, Exception exception, bool markFailed)
    {
        if (activity is null)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(exception);

        var message = TelemetryValueRedactor.RedactText(exception.Message);
        var tags = new TagList
        {
            { EksenSemanticConventions.ExceptionMessage, message },
            { EksenSemanticConventions.ExceptionStacktrace, TelemetryValueRedactor.RedactText(exception.ToString()) }
        };

        activity.AddException(exception, tags);

        if (markFailed)
        {
            activity.SetStatus(ActivityStatusCode.Error, message);
            activity.SetTag(EksenSemanticConventions.ErrorType, exception.GetType().FullName ?? exception.GetType().Name);
        }
    }

    public static void SetOutcome(this Activity? activity, string outcome)
    {
        activity?.SetTag(EksenSemanticConventions.EksenOutcome, outcome);
    }

    /// <summary>
    /// Builds a link to a stored W3C <c>traceparent</c>. Returns <see langword="null"/> when it is empty or invalid.
    /// </summary>
    public static ActivityLink? LinkFromTraceParent(string? traceParent)
    {
        if (string.IsNullOrWhiteSpace(traceParent))
        {
            return null;
        }

        return ActivityContext.TryParse(traceParent, traceState: null, isRemote: true, out var context)
            ? new ActivityLink(context)
            : null;
    }
}
