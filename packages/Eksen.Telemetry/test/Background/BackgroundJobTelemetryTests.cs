using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eksen.Telemetry.Background;
using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Eksen.Telemetry.Tests.Background;

public class BackgroundJobTelemetryTests : EksenUnitTestBase, IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener = new();
    private readonly List<Activity> _stopped = [];
    private readonly List<(string Instrument, double Value, Dictionary<string, object?> Tags)> _measurements = [];

    public BackgroundJobTelemetryTests()
    {
        var services = new ServiceCollection();
        services.AddEksenTelemetryPrimitives();
        _provider = services.BuildServiceProvider();
        Telemetry = _provider.GetRequiredService<BackgroundJobTelemetry>();

        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BackgroundJobTelemetry.Name,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (_stopped)
                {
                    _stopped.Add(activity);
                }
            }
        };
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter == Telemetry.Meter)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        _meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        _meterListener.Start();
    }

    private BackgroundJobTelemetry Telemetry { get; }

    [Fact]
    public void An_Idle_Iteration_Should_Emit_No_Activity()
    {
        // Act
        using (var iteration = Telemetry.StartIteration("outbox"))
        {
            iteration.Activity.ShouldBeNull();
        }

        // Assert
        Stopped().ShouldBeEmpty();
        Measurement("eksen.background.job.runs").Tags["eksen.job.result"].ShouldBe("idle");
        Measurement("eksen.background.job.duration").Tags["eksen.job.name"].ShouldBe("outbox");
    }

    [Fact]
    public void A_Worked_Iteration_Should_Emit_A_Root_Activity_Linked_To_Its_Origin()
    {
        // Arrange
        var link = EksenActivity.LinkFromTraceParent("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");
        using var ambient = new Activity("ambient").Start();

        // Act
        using (var iteration = Telemetry.StartIteration("webhooks", link))
        {
            iteration.MarkWorked().ShouldNotBeNull();
            iteration.MarkWorked().ShouldBe(iteration.Activity);
            iteration.AddItems(3);
        }

        // Assert
        var activity = Stopped().Single();
        activity.DisplayName.ShouldBe("webhooks");
        activity.ParentSpanId.ShouldBe(default);
        activity.TraceId.ShouldNotBe(ambient.TraceId);
        activity.Links.Single().Context.TraceId.ToHexString().ShouldBe("0af7651916cd43dd8448eb211c80319c");
        activity.GetTagItem("eksen.job.result").ShouldBe("succeeded");
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        Measurement("eksen.background.job.items").Value.ShouldBe(3);
        Measurement("eksen.background.job.runs").Tags["eksen.job.result"].ShouldBe("succeeded");
    }

    [Fact]
    public void A_Failed_Iteration_Should_Record_The_Exception()
    {
        // Act
        using (var iteration = Telemetry.StartIteration("builds"))
        {
            iteration.Fail(new InvalidOperationException("no runner"));
        }

        // Assert
        var activity = Stopped().Single();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").ShouldBe("System.InvalidOperationException");
        activity.GetTagItem("eksen.job.result").ShouldBe("failed");
        Measurement("eksen.background.job.runs").Tags["eksen.job.result"].ShouldBe("failed");
    }

    [Fact]
    public void Disposing_Twice_Should_Record_Once()
    {
        var iteration = Telemetry.StartIteration("twice");
        iteration.Dispose();
        iteration.Dispose();

        Measurements("eksen.background.job.runs").Count.ShouldBe(1);
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _meterListener.Dispose();
        _provider.Dispose();
    }

    private IReadOnlyList<Activity> Stopped()
    {
        lock (_stopped)
        {
            return _stopped.ToArray();
        }
    }

    private (double Value, Dictionary<string, object?> Tags) Measurement(string instrument)
    {
        return Measurements(instrument).Single();
    }

    private IReadOnlyList<(double Value, Dictionary<string, object?> Tags)> Measurements(string instrument)
    {
        lock (_measurements)
        {
            return _measurements.Where(m => m.Instrument == instrument).Select(m => (m.Value, m.Tags)).ToArray();
        }
    }

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var copy = new Dictionary<string, object?>();
        foreach (var (key, tagValue) in tags)
        {
            copy[key] = tagValue;
        }

        lock (_measurements)
        {
            _measurements.Add((instrument.Name, value, copy));
        }
    }
}
