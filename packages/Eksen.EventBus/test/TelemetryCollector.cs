using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Eksen.EventBus.Tests;

/// <summary>
/// Records the activities and measurements of one Eksen source and meter. Measurements can be limited to the
/// meter created by one <see cref="IMeterFactory"/>, so tests running in parallel do not see each other's values.
/// </summary>
internal sealed class TelemetryCollector : IDisposable
{
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;

    public TelemetryCollector(string name, IMeterFactory? meterScope = null)
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == name || source.Name.StartsWith("Eksen.", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => Activities.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == name
                    && (meterScope is null || ReferenceEquals(instrument.Meter.Scope, meterScope)))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        _meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Add(instrument, value, tags));
        _meterListener.SetMeasurementEventCallback<int>((instrument, value, tags, _) => Add(instrument, value, tags));
        _meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Add(instrument, value, tags));
        _meterListener.Start();
    }

    public ConcurrentQueue<Activity> Activities { get; } = new();

    public ConcurrentQueue<RecordedMeasurement> Measurements { get; } = new();

    public IReadOnlyList<Activity> ActivitiesIn(ActivityTraceId traceId)
    {
        return Activities.Where(activity => activity.TraceId == traceId).ToList();
    }

    public IReadOnlyList<RecordedMeasurement> MeasurementsOf(string instrument)
    {
        return Measurements.Where(measurement => measurement.Instrument == instrument).ToList();
    }

    public void RecordObservableInstruments()
    {
        _meterListener.RecordObservableInstruments();
    }

    public void Dispose()
    {
        _meterListener.Dispose();
        _activityListener.Dispose();
    }

    private void Add<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        where T : struct
    {
        Measurements.Enqueue(new RecordedMeasurement(
            instrument.Name,
            Convert.ToDouble(value),
            tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));
    }
}

internal sealed record RecordedMeasurement(string Instrument, double Value, IReadOnlyDictionary<string, object?> Tags)
{
    public object? Tag(string key)
    {
        return Tags.TryGetValue(key, out var value) ? value : null;
    }
}
