using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Eksen.Telemetry.Background;

/// <summary>
/// Spans and metrics for polling loops and background jobs (<c>Eksen.Background</c>). An iteration that finds
/// no work records metrics but never creates a span.
/// </summary>
public sealed class BackgroundJobTelemetry
{
    public const string Name = EksenTelemetry.Background;
    public const string IdleResult = "idle";
    public const string SucceededResult = "succeeded";
    public const string FailedResult = "failed";

    public static readonly ActivitySource ActivitySource = new(Name);

    public BackgroundJobTelemetry(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter = meterFactory.Create(Name);

        Runs = Meter.CreateCounter<long>(
            name: "eksen.background.job.runs",
            unit: "{run}",
            description: "Background job iterations. Tagged with eksen.job.name and eksen.job.result (idle, succeeded, failed).");

        Duration = Meter.CreateHistogram<double>(
            name: "eksen.background.job.duration",
            unit: "s",
            description: "Background job iteration duration. Tagged with eksen.job.name and eksen.job.result.");

        Items = Meter.CreateCounter<long>(
            name: "eksen.background.job.items",
            unit: "{item}",
            description: "Items processed by background job iterations. Tagged with eksen.job.name.");
    }

    public Meter Meter { get; }

    public Counter<long> Runs { get; }

    public Histogram<double> Duration { get; }

    public Counter<long> Items { get; }

    /// <summary>
    /// Starts measuring one iteration of <paramref name="job"/>. Dispose the returned value when the iteration
    /// ends; call <see cref="BackgroundJobIteration.MarkWorked"/> as soon as there is work to do.
    /// </summary>
    public BackgroundJobIteration StartIteration(string job, ActivityLink? link = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(job);

        return new BackgroundJobIteration(new BackgroundJobIteration.State(this, job, link));
    }
}

/// <summary>
/// One iteration of a background job. It is a handle over shared state, so it can be used with
/// <c>using var</c> and still be marked as worked or failed.
/// </summary>
public readonly struct BackgroundJobIteration : IDisposable
{
    private readonly State? _state;

    internal BackgroundJobIteration(State state)
    {
        _state = state;
    }

    /// <summary>
    /// The span of this iteration; <see langword="null"/> until <see cref="MarkWorked"/> is called (or when no
    /// listener samples it).
    /// </summary>
    public Activity? Activity
    {
        get { return _state?.Activity; }
    }

    public bool HasWorked
    {
        get { return _state?.Worked ?? false; }
    }

    /// <summary>
    /// Declares that this iteration found work and starts its root span (linked to the iteration's link).
    /// </summary>
    public Activity? MarkWorked()
    {
        return _state?.MarkWorked();
    }

    public void AddItems(long count)
    {
        _state?.AddItems(count);
    }

    /// <summary>
    /// Marks the iteration as failed and records <paramref name="exception"/> on its span.
    /// </summary>
    public void Fail(Exception exception)
    {
        _state?.Fail(exception);
    }

    public void Dispose()
    {
        _state?.Complete();
    }

    internal sealed class State(BackgroundJobTelemetry telemetry, string job, ActivityLink? link)
    {
        private readonly long _startTimestamp = Stopwatch.GetTimestamp();
        private bool _completed;
        private bool _failed;

        public Activity? Activity { get; private set; }

        public bool Worked { get; private set; }

        public Activity? MarkWorked()
        {
            if (Worked || _completed)
            {
                return Activity;
            }

            Worked = true;
            Activity = EksenActivity.StartRoot(
                BackgroundJobTelemetry.ActivitySource,
                job,
                ActivityKind.Internal,
                links: link is { } value ? [value] : null,
                tags: [new KeyValuePair<string, object?>(EksenSemanticConventions.EksenJobName, job)]);

            return Activity;
        }

        public void AddItems(long count)
        {
            if (count <= 0)
            {
                return;
            }

            telemetry.Items.Add(count, new KeyValuePair<string, object?>(EksenSemanticConventions.EksenJobName, job));
        }

        public void Fail(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            MarkWorked();
            _failed = true;
            Activity.RecordException(exception);
        }

        public void Complete()
        {
            if (_completed)
            {
                return;
            }

            _completed = true;

            var result = _failed
                ? BackgroundJobTelemetry.FailedResult
                : Worked
                    ? BackgroundJobTelemetry.SucceededResult
                    : BackgroundJobTelemetry.IdleResult;

            var tags = new TagList
            {
                { EksenSemanticConventions.EksenJobName, job },
                { EksenSemanticConventions.EksenJobResult, result }
            };

            telemetry.Runs.Add(1, tags);
            telemetry.Duration.Record(Stopwatch.GetElapsedTime(_startTimestamp).TotalSeconds, tags);

            if (Activity is not null)
            {
                Activity.SetTag(EksenSemanticConventions.EksenJobResult, result);
                if (!_failed)
                {
                    Activity.SetStatus(ActivityStatusCode.Ok);
                }

                Activity.Dispose();
            }
        }
    }
}
