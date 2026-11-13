using System.Diagnostics;
using System.Text;
using Eksen.Telemetry.Propagation;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Telemetry.Tests;

public class TraceContextPropagationTests : EksenUnitTestBase
{
    private const string TraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

    [Fact]
    public void Inject_Then_Extract_Should_Round_Trip_String_Headers()
    {
        // Arrange
        using var activity = CreateActivity();
        var headers = new Dictionary<string, string> { ["traceparent"] = "stale", ["x-other"] = "kept" };

        // Act
        TraceContextPropagation.Inject(activity, headers);
        var context = TraceContextPropagation.Extract(headers, out var baggage);

        // Assert
        headers["traceparent"].ShouldBe(activity.Id);
        headers["x-other"].ShouldBe("kept");
        context.TraceId.ShouldBe(activity.TraceId);
        context.SpanId.ShouldBe(activity.SpanId);
        context.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
        context.IsRemote.ShouldBeTrue();
        baggage.ShouldContain(item => item.Key == "tenant" && item.Value == "t-1");
    }

    [Fact]
    public void Inject_Should_Write_TraceState_And_Baggage()
    {
        // Arrange
        using var activity = CreateActivity();
        activity.TraceStateString = "vendor=value";
        var headers = new Dictionary<string, string>();

        // Act
        TraceContextPropagation.Inject(activity, headers);

        // Assert
        headers.ShouldContainKey("traceparent");
        headers["tracestate"].ShouldBe("vendor=value");
        headers["baggage"].Replace(" ", "").ShouldBe("tenant=t-1");
    }

    [Fact]
    public void Inject_With_Null_Activity_Should_Remove_Stale_Trace_Keys()
    {
        // Arrange
        var headers = new Dictionary<string, string> { ["traceparent"] = TraceParent, ["tracestate"] = "a=b", ["id"] = "1" };

        // Act
        TraceContextPropagation.Inject(null, headers);

        // Assert
        headers.Keys.ShouldBe(["id"]);
    }

    [Fact]
    public void ExtractFromAmqp_Should_Decode_Byte_Array_Values()
    {
        // Arrange
        var headers = new Dictionary<string, object?>
        {
            ["traceparent"] = Encoding.UTF8.GetBytes(TraceParent),
            ["tracestate"] = Encoding.UTF8.GetBytes("vendor=value")
        };

        // Act
        var context = TraceContextPropagation.ExtractFromAmqp(headers, out _);

        // Assert
        context.TraceId.ToHexString().ShouldBe("0af7651916cd43dd8448eb211c80319c");
        context.SpanId.ToHexString().ShouldBe("b7ad6b7169203331");
        context.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
        context.TraceState.ShouldBe("vendor=value");
        context.IsRemote.ShouldBeTrue();
    }

    [Fact]
    public void ExtractFromAmqp_Should_Accept_String_Values()
    {
        // Arrange
        var headers = new Dictionary<string, object?> { ["traceparent"] = TraceParent };

        // Act
        var context = TraceContextPropagation.ExtractFromAmqp(headers, out _);

        // Assert
        context.TraceId.ToHexString().ShouldBe("0af7651916cd43dd8448eb211c80319c");
    }

    [Theory]
    [InlineData("not-a-traceparent")]
    [InlineData("00-00000000000000000000000000000000-b7ad6b7169203331-01")]
    [InlineData("00-0af7651916cd43dd8448eb211c80319c-0000000000000000-01")]
    [InlineData("")]
    public void Invalid_TraceParent_Should_Return_Default(string traceParent)
    {
        // Arrange
        var stringHeaders = new Dictionary<string, string> { ["traceparent"] = traceParent };
        var amqpHeaders = new Dictionary<string, object?> { ["traceparent"] = Encoding.UTF8.GetBytes(traceParent) };

        // Act
        var fromStrings = TraceContextPropagation.Extract(stringHeaders, out _);
        var fromAmqp = TraceContextPropagation.ExtractFromAmqp(amqpHeaders, out _);

        // Assert
        fromStrings.ShouldBe(default);
        fromAmqp.ShouldBe(default);
    }

    [Fact]
    public void Missing_Headers_Should_Return_Default()
    {
        TraceContextPropagation.Extract(null, out var baggage).ShouldBe(default);
        baggage.ShouldBeEmpty();
        TraceContextPropagation.ExtractFromAmqp(null, out _).ShouldBe(default);
        TraceContextPropagation.ExtractFromAmqp(new Dictionary<string, object?> { ["other"] = 42 }, out _).ShouldBe(default);
    }

    private static Activity CreateActivity()
    {
        var activity = new Activity("propagation-test");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.AddBaggage("tenant", "t-1");
        activity.Start();
        return activity;
    }
}
