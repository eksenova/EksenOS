using System.Diagnostics;
using Eksen.Telemetry;
using Eksen.TestBase;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Eksen.EventBus.InMemory.Tests;

public class InMemoryTransportTelemetryTests : EksenUnitTestBase
{
    private static readonly ActivitySource TestSource = new("Eksen.EventBus.InMemory.Tests");

    [Fact]
    public async Task The_Channel_Hop_Keeps_The_Publishers_Trace_With_A_Consumer_Span()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.EventBus);
        var processed = new TaskCompletionSource<Activity?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processor = new Mock<IEventProcessor>();
        processor.Setup(p => p.ProcessAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => processed.TrySetResult(Activity.Current))
            .Returns(Task.CompletedTask);
        var transport = new InMemoryEventBusTransport(processor.Object, NullLogger<InMemoryEventBusTransport>.Instance);
        var listening = Task.Run(() => transport.StartListeningAsync(), TestContext.Current.CancellationToken);

        ActivityTraceId publisherTrace;
        ActivitySpanId publisherSpan;
        using (var publisher = TestSource.StartActivity("publisher")!)
        {
            publisherTrace = publisher.TraceId;
            publisherSpan = publisher.SpanId;
            await transport.PublishAsync("Orders.Created", "{}", "corr-1", "Api", null, Guid.NewGuid(), null,
                TestContext.Current.CancellationToken);
        }

        var current = await processed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await transport.StopListeningAsync(TestContext.Current.CancellationToken);
        await Should.ThrowAsync<OperationCanceledException>(() => listening);

        current.ShouldNotBeNull();
        current.DisplayName.ShouldBe("process Orders.Created");
        current.Kind.ShouldBe(ActivityKind.Consumer);
        current.TraceId.ShouldBe(publisherTrace);
        current.ParentSpanId.ShouldBe(publisherSpan);
        current.GetTagItem("messaging.system").ShouldBe("in_memory");
        current.GetTagItem("messaging.message.conversation_id").ShouldBe("corr-1");
        current.GetTagItem("eksen.source_app").ShouldBe("Api");
    }

    [Fact]
    public async Task Publish_Does_Not_Mutate_The_Callers_Headers()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.EventBus);
        var transport = new InMemoryEventBusTransport(new Mock<IEventProcessor>().Object, NullLogger<InMemoryEventBusTransport>.Instance);
        var headers = new Dictionary<string, string> { ["tenant"] = "t-1" };

        using var publisher = TestSource.StartActivity("publisher")!;
        await transport.PublishAsync("Orders.Created", "{}", null, null, null, Guid.NewGuid(), headers,
            TestContext.Current.CancellationToken);

        headers.Keys.ShouldBe(["tenant"]);
    }
}
