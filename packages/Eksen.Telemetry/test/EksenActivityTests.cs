using System.Diagnostics;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Telemetry.Tests;

public class EksenActivityTests : EksenUnitTestBase, IDisposable
{
    private readonly ActivitySource _source = new("Eksen.Tests." + Guid.NewGuid().ToString("N"));
    private readonly ActivityListener _listener;

    public EksenActivityTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == _source.Name,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [Fact]
    public void StartRoot_Should_Ignore_The_Ambient_Activity()
    {
        // Arrange
        using var ambient = _source.StartActivity("ambient");
        ambient.ShouldNotBeNull();

        // Act
        using var root = EksenActivity.StartRoot(_source, "root", ActivityKind.Consumer);

        // Assert
        root.ShouldNotBeNull();
        root.Parent.ShouldBeNull();
        root.ParentSpanId.ShouldBe(default);
        root.TraceId.ShouldNotBe(ambient.TraceId);
        root.Kind.ShouldBe(ActivityKind.Consumer);
    }

    [Fact]
    public void StartRoot_Should_Keep_The_Ambient_Activity_When_Nothing_Listens()
    {
        // Arrange
        using var ambient = _source.StartActivity("ambient");
        using var unheard = new ActivitySource("Eksen.Tests.Unheard");

        // Act
        var root = EksenActivity.StartRoot(unheard, "root", ActivityKind.Internal);

        // Assert
        root.ShouldBeNull();
        Activity.Current.ShouldBe(ambient);
    }

    [Fact]
    public void StartRoot_Should_Attach_Links_And_Tags()
    {
        // Arrange
        var link = EksenActivity.LinkFromTraceParent("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");

        // Act
        using var root = EksenActivity.StartRoot(
            _source,
            "root",
            ActivityKind.Internal,
            links: [link!.Value],
            tags: [new KeyValuePair<string, object?>("k", "v")]);

        // Assert
        root.ShouldNotBeNull();
        root.Links.Single().Context.TraceId.ToHexString().ShouldBe("0af7651916cd43dd8448eb211c80319c");
        root.GetTagItem("k").ShouldBe("v");
    }

    [Fact]
    public void RecordException_Should_Mark_The_Activity_As_Failed()
    {
        // Arrange
        using var activity = _source.StartActivity("work");
        var exception = new InvalidOperationException("broken");

        // Act
        activity.RecordException(exception);

        // Assert
        activity!.Status.ShouldBe(ActivityStatusCode.Error);
        activity.StatusDescription.ShouldBe("broken");
        activity.GetTagItem("error.type").ShouldBe("System.InvalidOperationException");
        activity.Events.ShouldContain(e => e.Name == "exception");
    }

    [Fact]
    public void RecordException_Should_Redact_Secrets_In_The_Message()
    {
        // Arrange
        using var activity = _source.StartActivity("work");
        var exception = new HttpRequestException("GET https://api.test/v1/items?access_token=SECRET failed");

        // Act
        activity.RecordException(exception);

        // Assert
        activity!.StatusDescription.ShouldBe("GET https://api.test/v1/items?access_token=[redacted] failed");
        var exceptionEvent = activity.Events.Single(e => e.Name == "exception");
        foreach (var (_, value) in exceptionEvent.Tags)
        {
            (value?.ToString() ?? string.Empty).ShouldNotContain("SECRET");
        }

        exceptionEvent.Tags.ShouldContain(tag => tag.Key == "exception.type");
    }

    [Fact]
    public void SetOutcome_And_Null_Activity_Should_Not_Throw()
    {
        Activity? missing = null;
        missing.SetOutcome("committed");
        missing.RecordException(new Exception());

        using var activity = _source.StartActivity("work");
        activity.SetOutcome("committed");
        activity!.GetTagItem("eksen.outcome").ShouldBe("committed");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    public void LinkFromTraceParent_Should_Return_Null_For_Invalid_Input(string? traceParent)
    {
        EksenActivity.LinkFromTraceParent(traceParent).ShouldBeNull();
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }
}
