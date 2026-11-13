using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using Eksen.Telemetry;
using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shouldly;

namespace Eksen.EventBus.RabbitMq.Tests;

public class RabbitMqTelemetryTests : EksenUnitTestBase
{
    private static readonly ActivitySource TestSource = new("Eksen.EventBus.RabbitMq.Tests");

    [Fact]
    public async Task Consumer_Span_Is_Parented_On_The_Header_Context_And_Is_Current_For_The_Processor()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.EventBusRabbitMq);
        var remote = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        Activity? currentInProcessor = null;
        var processor = new Mock<IEventProcessor>();
        processor.Setup(p => p.ProcessAsync(It.IsAny<EventDelivery>(), It.IsAny<CancellationToken>()))
            .Callback(() => currentInProcessor = Activity.Current)
            .ReturnsAsync(EventProcessingOutcome.Processed);
        var channel = new Mock<IChannel>();
        var transport = CreateTransport(processor.Object);
        var messageId = Guid.NewGuid();
        var delivery = CreateDelivery(
            messageId,
            new Dictionary<string, object?>
            {
                ["x-event-type"] = Encoding.UTF8.GetBytes("Orders.Created"),
                ["x-source-app"] = Encoding.UTF8.GetBytes("Api"),
                ["traceparent"] = Encoding.UTF8.GetBytes($"00-{remote.TraceId.ToHexString()}-{remote.SpanId.ToHexString()}-01")
            });

        await transport.HandleDeliveryAsync(channel.Object, delivery, "App.Handler", Descriptor, CancellationToken.None);

        var span = collector.ActivitiesIn(remote.TraceId).Single(activity => activity.DisplayName == "process orders.created");
        span.Kind.ShouldBe(ActivityKind.Consumer);
        span.ParentSpanId.ShouldBe(remote.SpanId);
        span.GetTagItem("messaging.system").ShouldBe("rabbitmq");
        span.GetTagItem("messaging.destination.name").ShouldBe("App.Handler");
        span.GetTagItem("messaging.rabbitmq.destination.routing_key").ShouldBe("orders.created");
        span.GetTagItem("messaging.message.id").ShouldBe(messageId.ToString());
        span.GetTagItem("messaging.message.conversation_id").ShouldBe("corr-1");
        span.GetTagItem("eksen.source_app").ShouldBe("Api");
        currentInProcessor.ShouldBeSameAs(span);
        channel.Verify(c => c.BasicAckAsync(7, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consumer_Failure_Marks_The_Span_Failed_And_Nacks()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.EventBusRabbitMq);
        var remote = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        var processor = new Mock<IEventProcessor>();
        processor.Setup(p => p.ProcessAsync(It.IsAny<EventDelivery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("handler failed"));
        var channel = new Mock<IChannel>();
        var delivery = CreateDelivery(
            Guid.NewGuid(),
            new Dictionary<string, object?>
            {
                ["traceparent"] = $"00-{remote.TraceId.ToHexString()}-{remote.SpanId.ToHexString()}-01"
            });

        // Without delayed redelivery or dead-letter queues a failed delivery is rejected.
        await CreateTransport(
                processor.Object,
                new RabbitMqEventBusOptions { DeclareDeadLetterQueues = false },
                new EksenEventBusOptions { Redelivery = new RedeliveryOptions { IsEnabled = false } })
            .HandleDeliveryAsync(channel.Object, delivery, "App.Handler", Descriptor, CancellationToken.None);

        var span = collector.ActivitiesIn(remote.TraceId).ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.GetTagItem("error.type").ShouldBe(typeof(InvalidOperationException).FullName);
        channel.Verify(c => c.BasicNackAsync(7, false, false, It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consumer_Without_Trace_Headers_Starts_A_New_Root()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.EventBusRabbitMq);
        using var listenerLoop = TestSource.StartActivity("listener loop")!;
        Activity? currentInProcessor = null;
        var processor = new Mock<IEventProcessor>();
        processor.Setup(p => p.ProcessAsync(It.IsAny<EventDelivery>(), It.IsAny<CancellationToken>()))
            .Callback(() => currentInProcessor = Activity.Current)
            .ReturnsAsync(EventProcessingOutcome.Processed);

        await CreateTransport(processor.Object).HandleDeliveryAsync(
            new Mock<IChannel>().Object,
            CreateDelivery(Guid.NewGuid(), new Dictionary<string, object?>()),
            "App.Handler",
            Descriptor,
            CancellationToken.None);

        currentInProcessor.ShouldNotBeNull();
        currentInProcessor.TraceId.ShouldNotBe(listenerLoop.TraceId);
        currentInProcessor.ParentSpanId.ShouldBe(default);
    }

    [Fact]
    public async Task PublishAsync_Injects_The_Current_Span_When_Headers_Have_No_Traceparent()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.EventBusRabbitMq);
        var (transport, channel) = CreatePublishingTransport();
        BasicProperties? published = null;
        channel.Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>(
                (_, _, _, properties, _, _) => published = properties)
            .Returns(ValueTask.CompletedTask);

        using var request = TestSource.StartActivity("request")!;
        await transport.PublishAsync("Orders.Created", "{}", null, null, null, Guid.NewGuid(),
            new Dictionary<string, string> { ["tenant"] = "t-1" }, TestContext.Current.CancellationToken);

        published.ShouldNotBeNull();
        published.Headers!["tenant"].ShouldBe("t-1");
        ((string)published.Headers["traceparent"]!).ShouldContain(request.TraceId.ToHexString());
        ((string)published.Headers["traceparent"]!).ShouldContain(request.SpanId.ToHexString());
    }

    [Fact]
    public async Task PublishAsync_Keeps_A_Traceparent_Already_In_The_Headers()
    {
        var (transport, channel) = CreatePublishingTransport();
        BasicProperties? published = null;
        channel.Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>(
                (_, _, _, properties, _, _) => published = properties)
            .Returns(ValueTask.CompletedTask);
        const string stored = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

        using var collector = new TelemetryCollector(EksenTelemetry.EventBusRabbitMq);
        using var request = TestSource.StartActivity("request")!;
        await transport.PublishAsync("Orders.Created", "{}", null, null, null, Guid.NewGuid(),
            new Dictionary<string, string> { ["traceparent"] = stored }, TestContext.Current.CancellationToken);

        published!.Headers!["traceparent"].ShouldBe(stored);
    }

    [Fact]
    public async Task Rescheduler_Carries_The_Current_Traceparent_To_The_Delay_Queue()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.EventBusRabbitMq);
        var (transport, channel, connectionManager) = CreateTransportWithChannel();
        BasicProperties? published = null;
        channel.Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>(
                (_, _, _, properties, _, _) => published = properties)
            .Returns(ValueTask.CompletedTask);
        var rescheduler = new RabbitMqEventRescheduler(transport);

        using var handler = TestSource.StartActivity("handler")!;
        await rescheduler.RescheduleAsync(
            new EventRedelivery { EventTypeName = "Orders.Created", Payload = "{}", EventId = Guid.NewGuid(), HandlerTypeName = "Orders.Handler", CorrelationId = "corr-1", SourceApp = "src" },
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        published.ShouldNotBeNull();
        var traceParent = (string)published.Headers!["traceparent"]!;
        traceParent.ShouldBe($"00-{handler.TraceId.ToHexString()}-{handler.SpanId.ToHexString()}-01");
        published.Headers[RabbitMqHeaders.DelayTier].ShouldBe("1");
        published.Expiration.ShouldBe("5000");
    }

    [Fact]
    public async Task Rescheduler_Without_Delay_Publishes_With_The_Current_Traceparent()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.EventBusRabbitMq);
        var (transport, channel, connectionManager) = CreateTransportWithChannel();
        BasicProperties? published = null;
        channel.Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>(
                (_, _, _, properties, _, _) => published = properties)
            .Returns(ValueTask.CompletedTask);
        var rescheduler = new RabbitMqEventRescheduler(transport);

        using var handler = TestSource.StartActivity("handler")!;
        await rescheduler.RescheduleAsync(
            new EventRedelivery { EventTypeName = "Orders.Created", Payload = "{}", EventId = Guid.NewGuid(), HandlerTypeName = "Orders.Handler" },
            TimeSpan.Zero,
            TestContext.Current.CancellationToken);

        ((string)published!.Headers!["traceparent"]!).ShouldContain(handler.SpanId.ToHexString());
    }

    [Fact]
    public void Connection_Gauge_Reports_Down_Before_The_First_Connection()
    {
        var meterFactory = new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();
        using var collector = new TelemetryCollector(EksenTelemetry.EventBusRabbitMq, meterFactory);
        var manager = new RabbitMqConnectionManager(
            Options.Create(new RabbitMqEventBusOptions()),
            NullLogger<RabbitMqConnectionManager>.Instance,
            meterFactory);

        collector.RecordObservableInstruments();

        manager.IsConnected.ShouldBeFalse();
        collector.MeasurementsOf("eksen.rabbitmq.connection.up").ShouldHaveSingleItem().Value.ShouldBe(0);
    }

    [Fact]
    public void Reconnects_Are_Counted_By_Kind()
    {
        var meterFactory = new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();
        using var collector = new TelemetryCollector(EksenTelemetry.EventBusRabbitMq, meterFactory);
        var manager = new RabbitMqConnectionManager(
            Options.Create(new RabbitMqEventBusOptions()),
            NullLogger<RabbitMqConnectionManager>.Instance,
            meterFactory);

        manager.RecordReconnect("automatic_recovery");

        collector.MeasurementsOf("eksen.rabbitmq.reconnects").ShouldHaveSingleItem()
            .Tag("eksen.rabbitmq.reconnect.kind").ShouldBe("automatic_recovery");
    }

    [Fact]
    public async Task UseRabbitMq_Registers_The_Source_And_Meter_With_A_Valid_Container()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEksen(eksen => eksen.AddEventBus(bus => bus.UseRabbitMq()));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        Eksen.Telemetry.Registry.EksenTelemetryRegistry.GetSourceNames(provider).ShouldContain(EksenTelemetry.EventBusRabbitMq);
        Eksen.Telemetry.Registry.EksenTelemetryRegistry.GetMeterNames(provider).ShouldContain(EksenTelemetry.EventBusRabbitMq);
        provider.GetRequiredService<IRabbitMqConnectionManager>().ShouldBeOfType<RabbitMqConnectionManager>();
    }

    private static readonly EventHandlerDescriptor Descriptor = new()
    {
        EventType = typeof(OrdersCreated),
        HandlerType = typeof(OrdersCreatedHandler)
    };

    private static RabbitMqEventBusTransport CreateTransport(
        IEventProcessor processor,
        RabbitMqEventBusOptions? rabbitOptions = null,
        EksenEventBusOptions? busOptions = null)
    {
        return new RabbitMqEventBusTransport(
            new Mock<IRabbitMqConnectionManager>().Object,
            processor,
            new Mock<IEventHandlerRegistry>().Object,
            Options.Create(rabbitOptions ?? new RabbitMqEventBusOptions()),
            Options.Create(busOptions ?? new EksenEventBusOptions()),
            NullLogger<RabbitMqEventBusTransport>.Instance);
    }

    public sealed class OrdersCreated : IntegrationEvent;

    public sealed class OrdersCreatedHandler : IEventHandler<OrdersCreated>
    {
        public Task HandleAsync(OrdersCreated @event, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private static (RabbitMqEventBusTransport Transport, Mock<IChannel> Channel) CreatePublishingTransport()
    {
        var (transport, channel, _) = CreateTransportWithChannel();
        return (transport, channel);
    }

    private static (RabbitMqEventBusTransport Transport, Mock<IChannel> Channel, Mock<IRabbitMqConnectionManager> ConnectionManager)
        CreateTransportWithChannel()
    {
        var channel = new Mock<IChannel>();
        channel.SetupGet(c => c.IsOpen).Returns(true);
        var connection = new Mock<IConnection>();
        connection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel.Object);
        var connectionManager = new Mock<IRabbitMqConnectionManager>();
        connectionManager.Setup(cm => cm.GetConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection.Object);

        var transport = new RabbitMqEventBusTransport(
            connectionManager.Object,
            new Mock<IEventProcessor>().Object,
            new Mock<IEventHandlerRegistry>().Object,
            Options.Create(new RabbitMqEventBusOptions()),
            Options.Create(new EksenEventBusOptions()),
            NullLogger<RabbitMqEventBusTransport>.Instance);
        return (transport, channel, connectionManager);
    }

    private static BasicDeliverEventArgs CreateDelivery(Guid messageId, IDictionary<string, object?> headers)
    {
        var properties = new BasicProperties
        {
            MessageId = messageId.ToString(),
            CorrelationId = "corr-1",
            Headers = headers
        };

        return new BasicDeliverEventArgs(
            consumerTag: "consumer-1",
            deliveryTag: 7,
            redelivered: false,
            exchange: "eksen.events",
            routingKey: "orders.created",
            properties: properties,
            body: Encoding.UTF8.GetBytes("{}"),
            cancellationToken: CancellationToken.None);
    }
}
