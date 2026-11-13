using System.Diagnostics;
using Eksen.TestBase;
using OpenTelemetry.Trace;
using Shouldly;

namespace Eksen.Telemetry.OpenTelemetry.Tests;

public class EksenSamplerTests : EksenUnitTestBase
{
    private static readonly ActivityTraceId TraceId = ActivityTraceId.CreateRandom();

    [Fact]
    public void A_Sampled_Remote_Parent_Cannot_Force_Sampling()
    {
        var parent = new ActivityContext(TraceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true);

        Decide(EksenSampler.Create(0), parent).ShouldBe(SamplingDecision.Drop);
    }

    [Fact]
    public void An_Unsampled_Remote_Parent_Cannot_Suppress_Sampling()
    {
        var parent = new ActivityContext(TraceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, isRemote: true);

        Decide(EksenSampler.Create(1), parent).ShouldBe(SamplingDecision.RecordAndSample);
    }

    [Fact]
    public void Local_Children_Follow_Their_Parent()
    {
        var sampledParent = new ActivityContext(TraceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: false);
        var droppedParent = new ActivityContext(TraceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, isRemote: false);

        Decide(EksenSampler.Create(0), sampledParent).ShouldBe(SamplingDecision.RecordAndSample);
        Decide(EksenSampler.Create(1), droppedParent).ShouldBe(SamplingDecision.Drop);
    }

    [Fact]
    public void Roots_Use_The_Ratio()
    {
        Decide(EksenSampler.Create(1), default).ShouldBe(SamplingDecision.RecordAndSample);
        Decide(EksenSampler.Create(0), default).ShouldBe(SamplingDecision.Drop);
    }

    [Fact]
    public void Remote_Decisions_Are_Consistent_With_Roots_For_The_Same_Trace_Id()
    {
        var sampler = EksenSampler.Create(0.5);

        for (var i = 0; i < 200; i++)
        {
            var traceId = ActivityTraceId.CreateRandom();
            var root = sampler.ShouldSample(new SamplingParameters(default, traceId, "op", ActivityKind.Server)).Decision;
            var remote = sampler.ShouldSample(new SamplingParameters(
                new ActivityContext(traceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true),
                traceId,
                "op",
                ActivityKind.Server)).Decision;

            remote.ShouldBe(root);
        }
    }

    [Fact]
    public void Root_Client_Spans_Are_Dropped_Unless_Enabled()
    {
        var root = new SamplingParameters(default, TraceId, "SELECT", ActivityKind.Client);
        var child = new SamplingParameters(
            new ActivityContext(TraceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: false),
            TraceId,
            "SELECT",
            ActivityKind.Client);

        EksenSampler.Create(1).ShouldSample(root).Decision.ShouldBe(SamplingDecision.Drop);
        EksenSampler.Create(1).ShouldSample(child).Decision.ShouldBe(SamplingDecision.RecordAndSample);
        EksenSampler.Create(1, traceOrphanClientSpans: true).ShouldSample(root).Decision.ShouldBe(SamplingDecision.RecordAndSample);
        EksenSampler.Create(1).ShouldSample(new SamplingParameters(default, TraceId, "poll", ActivityKind.Internal)).Decision
            .ShouldBe(SamplingDecision.RecordAndSample);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void Invalid_Ratios_Are_Rejected(double ratio)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => EksenSampler.Create(ratio));
    }

    private static SamplingDecision Decide(Sampler sampler, ActivityContext parent)
    {
        return sampler.ShouldSample(new SamplingParameters(parent, TraceId, "operation", ActivityKind.Server)).Decision;
    }
}
