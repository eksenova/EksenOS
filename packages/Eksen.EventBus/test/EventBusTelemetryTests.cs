using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eksen.EventBus.BackgroundServices;
using Eksen.EventBus.DeadLetter;
using Eksen.EventBus.Inbox;
using Eksen.EventBus.Outbox;
using Eksen.EventBus.Retry;
using Eksen.EventBus.Telemetry;
using Eksen.Telemetry;
using Eksen.TestBase;
using Eksen.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Polly;
using Shouldly;

namespace Eksen.EventBus.Tests;

public class EventBusTelemetryTests : EksenUnitTestBase
{
    private static readonly ActivitySource TestSource = new("Eksen.EventBus.Tests");

    private static IMeterFactory CreateMeterFactory()
    {
        return new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();
    }

    private static string? TraceParentOf(IReadOnlyDictionary<string, string>? headers)
    {
        return headers != null && headers.TryGetValue("traceparent", out var value) ? value : null;
    }

    private static ActivityContext ParseTraceParent(string traceParent)
    {
        ActivityContext.TryParse(traceParent, null, isRemote: true, out var context).ShouldBeTrue();
        return context;
    }

    [Fact]
    public async Task PublishAsync_Stores_The_Callers_Trace_Context_In_The_Outbox_Headers()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.EventBus);
        OutboxMessage? saved = null;
        var outboxStore = new Mock<IOutboxStore>();
        outboxStore.Setup(x => x.SaveAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, CancellationToken>((message, _) => saved = message)
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(outboxStore.Object);
        var provider = services.BuildServiceProvider();
        var bus = new DefaultEventBus(
            new Mock<IEventBusTransport>().Object,
            new JsonEventSerializer(),
            Options.Create(new EksenEventBusOptions { AppName = "TestApp" }),
            provider.GetRequiredService<IServiceScopeFactory>(),
            new LocalEventDispatcher(provider));

        using var request = TestSource.StartActivity("request", ActivityKind.Server)!;
        await bus.PublishAsync(
            new TestOrderCreatedEvent { OrderNumber = "ORD-1" },
            new PublishOptions { Headers = new Dictionary<string, string> { ["tenant"] = "t-1" } });

        saved.ShouldNotBeNull();
        saved.Headers.ShouldNotBeNull();
        saved.Headers["tenant"].ShouldBe("t-1");
        var stored = ParseTraceParent(TraceParentOf(saved.Headers)!);
        stored.TraceId.ShouldBe(request.TraceId);

        var create = collector.ActivitiesIn(request.TraceId)
            .Single(activity => activity.DisplayName == $"create {EventNameResolver.GetEventName<TestOrderCreatedEvent>()}");
        create.Kind.ShouldBe(ActivityKind.Producer);
        create.ParentSpanId.ShouldBe(request.SpanId);
        stored.SpanId.ShouldBe(create.SpanId);
    }

    [Fact]
    public async Task UnitOfWorkEventBus_Captures_The_Trace_Context_At_Publish_Not_At_Flush()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.EventBus);
        OutboxMessage? saved = null;
        var outboxStore = new Mock<IOutboxStore>();
        outboxStore.Setup(x => x.SaveAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, CancellationToken>((message, _) => saved = message)
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton<IEnumerable<IUnitOfWorkProvider>>(Array.Empty<IUnitOfWorkProvider>());
        services.AddSingleton(outboxStore.Object);
        var provider = services.BuildServiceProvider();
        var manager = new UnitOfWorkManager(provider);
        var options = Options.Create(new EksenEventBusOptions { AppName = "TestApp" });
        var inner = new DefaultEventBus(
            new Mock<IEventBusTransport>().Object,
            new JsonEventSerializer(),
            options,
            provider.GetRequiredService<IServiceScopeFactory>(),
            new LocalEventDispatcher(provider));
        var bus = new UnitOfWorkEventBus(inner, manager, new JsonEventSerializer(), options);

        var scope = manager.BeginScope();
        ActivityTraceId publishTrace;
        using (var publishing = TestSource.StartActivity("publishing")!)
        {
            publishTrace = publishing.TraceId;
            await bus.PublishAsync(
                new TestOrderCreatedEvent { OrderNumber = "ORD-2" },
                new PublishOptions { DispatchMode = EventDispatchMode.AfterUnitOfWork });
        }

        saved.ShouldBeNull();
        using (var flushing = TestSource.StartActivity("flushing")!)
        {
            flushing.TraceId.ShouldNotBe(publishTrace);
            await scope.CommitAsync();
        }

        saved.ShouldNotBeNull();
        ParseTraceParent(TraceParentOf(saved.Headers)!).TraceId.ShouldBe(publishTrace);
    }

    [Fact]
    public async Task Outbox_Publish_Span_Is_Parented_On_The_Stored_Context_And_Linked_To_The_Poll_Span()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TelemetryCollector(EksenTelemetry.EventBus, meterFactory);
        var stored = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = "Orders.Created",
            Payload = "{}",
            CreationTime = DateTime.UtcNow.AddSeconds(-3),
            Status = OutboxMessageStatus.Pending,
            Headers = new Dictionary<string, string>
            {
                ["traceparent"] = $"00-{stored.TraceId.ToHexString()}-{stored.SpanId.ToHexString()}-01",
                ["tenant"] = "t-1"
            }
        };

        var outboxStore = new Mock<IOutboxStore>();
        outboxStore.Setup(x => x.ClaimAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([message]);
        Dictionary<string, string>? sentHeaders = null;
        Guid? sentMessageId = null;
        var transport = new Mock<IEventBusTransport>();
        transport.Setup(x => x.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<Guid>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, string?, string?, Guid, Dictionary<string, string>?, CancellationToken>(
                (_, _, _, _, _, eventId, headers, _) =>
                {
                    sentHeaders = headers;
                    sentMessageId = eventId;
                })
            .Returns(Task.CompletedTask);

        var processor = CreateOutboxProcessor(outboxStore.Object, transport.Object, meterFactory);
        await processor.ProcessBatchAsync(new OutboxOptions(), CancellationToken.None);

        var publish = collector.ActivitiesIn(stored.TraceId).Single(activity => activity.DisplayName == "publish Orders.Created");
        publish.Kind.ShouldBe(ActivityKind.Producer);
        publish.ParentSpanId.ShouldBe(stored.SpanId);
        publish.GetTagItem("messaging.message.id").ShouldBe(message.EventId.ToString());

        var poll = collector.Activities.Single(activity =>
            activity.DisplayName == "outbox poll" && publish.Links.Any(link => link.Context.SpanId == activity.SpanId));
        poll.ParentSpanId.ShouldBe(default);

        // The broker message id is the event's own id, not the outbox row id.
        sentMessageId.ShouldBe(message.EventId);
        sentHeaders.ShouldNotBeNull();
        sentHeaders["tenant"].ShouldBe("t-1");
        var sentContext = ParseTraceParent(TraceParentOf(sentHeaders)!);
        sentContext.TraceId.ShouldBe(stored.TraceId);
        sentContext.SpanId.ShouldBe(publish.SpanId);
        message.Headers["traceparent"].ShouldContain(stored.SpanId.ToHexString());

        var sent = collector.MeasurementsOf("messaging.client.sent.messages").ShouldHaveSingleItem();
        sent.Tag("outcome").ShouldBe("ok");
        sent.Tag("eksen.event.type").ShouldBe("Orders.Created");
        collector.MeasurementsOf("eksen.eventbus.outbox.lag").ShouldHaveSingleItem().Value.ShouldBeGreaterThanOrEqualTo(3);
        collector.MeasurementsOf("messaging.client.operation.duration").ShouldHaveSingleItem();
        outboxStore.Verify(x => x.MarkAsProcessedAsync(message.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Outbox_Idle_Poll_Emits_No_Span()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TelemetryCollector(EksenTelemetry.EventBus, meterFactory);
        var outboxStore = new Mock<IOutboxStore>();
        outboxStore.Setup(x => x.ClaimAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        using var marker = TestSource.StartActivity("marker")!;
        var processor = CreateOutboxProcessor(outboxStore.Object, new Mock<IEventBusTransport>().Object, meterFactory);
        await processor.ProcessBatchAsync(new OutboxOptions(), CancellationToken.None);

        collector.ActivitiesIn(marker.TraceId).ShouldBeEmpty();
        collector.Measurements.ShouldBeEmpty();
    }

    [Fact]
    public async Task Outbox_Marks_Failed_When_The_Incremented_Retry_Count_Reaches_The_Limit()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TelemetryCollector(EksenTelemetry.EventBus, meterFactory);
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = "Orders.Created",
            Payload = "{}",
            CreationTime = DateTime.UtcNow,
            Status = OutboxMessageStatus.Processing,
            RetryCount = new OutboxOptions().MaxPublishAttempts - 1
        };
        var outboxStore = new Mock<IOutboxStore>();
        outboxStore.Setup(x => x.ClaimAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([message]);
        var transport = CreateFailingTransport();

        var processor = CreateOutboxProcessor(outboxStore.Object, transport.Object, meterFactory);
        await processor.ProcessBatchAsync(new OutboxOptions(), CancellationToken.None);

        outboxStore.Verify(x => x.IncrementRetryCountAsync(message.Id, It.IsAny<CancellationToken>()), Times.Once);
        outboxStore.Verify(x => x.MarkAsFailedAsync(message.Id, "broker down", It.IsAny<CancellationToken>()), Times.Once);
        outboxStore.Verify(x => x.ScheduleRetryAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        collector.MeasurementsOf("messaging.client.sent.messages").ShouldHaveSingleItem().Tag("outcome").ShouldBe("failed");
        collector.MeasurementsOf("messaging.client.operation.duration").ShouldHaveSingleItem()
            .Tag("error.type").ShouldBe(typeof(InvalidOperationException).FullName);

        var publish = collector.Activities.Single(activity =>
            activity.DisplayName == "publish Orders.Created" && (string?)activity.GetTagItem("messaging.message.id") == message.EventId.ToString());
        publish.Status.ShouldBe(ActivityStatusCode.Error);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 10)]
    [InlineData(3, 40)]
    public async Task Outbox_Failed_Publish_Backs_Off_Exponentially_With_Jitter(int previousAttempts, int nominalSeconds)
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = "Orders.Created",
            Payload = "{}",
            CreationTime = DateTime.UtcNow,
            Status = OutboxMessageStatus.Processing,
            RetryCount = previousAttempts
        };
        DateTime? nextAttempt = null;
        var outboxStore = new Mock<IOutboxStore>();
        outboxStore.Setup(x => x.ClaimAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([message]);
        outboxStore.Setup(x => x.ScheduleRetryAsync(message.Id, It.IsAny<DateTime>(), "broker down", It.IsAny<CancellationToken>()))
            .Callback<Guid, DateTime, string, CancellationToken>((_, next, _, _) => nextAttempt = next)
            .Returns(Task.CompletedTask);

        var startedAt = DateTime.UtcNow;
        var processor = CreateOutboxProcessor(outboxStore.Object, CreateFailingTransport().Object, CreateMeterFactory());
        await processor.ProcessBatchAsync(new OutboxOptions(), CancellationToken.None);

        nextAttempt.ShouldNotBeNull();
        var delay = (nextAttempt.Value - startedAt).TotalSeconds;
        delay.ShouldBeGreaterThanOrEqualTo(nominalSeconds * 0.8 - 0.5);
        delay.ShouldBeLessThanOrEqualTo(nominalSeconds * 1.2 + 0.5);
        outboxStore.Verify(x => x.MarkAsFailedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        outboxStore.Verify(x => x.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IEventBusTransport> CreateFailingTransport()
    {
        var transport = new Mock<IEventBusTransport>();
        transport.Setup(x => x.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<Guid>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker down"));
        return transport;
    }

    [Fact]
    public async Task Handler_Span_Is_A_Child_Of_The_Current_Span_And_Counts_A_Processed_Message()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TelemetryCollector(EksenTelemetry.EventBus, meterFactory);
        var processor = CreateProcessor<SucceedingHandler>(services => services.AddSingleton<SucceedingHandler>(), meterFactory);
        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-3" };

        using var consumer = TestSource.StartActivity("consumer", ActivityKind.Consumer)!;
        await processor.ProcessAsync(EventName, new JsonEventSerializer().Serialize(@event), null, null, @event.EventId);

        var handlerName = typeof(SucceedingHandler).FullName!;
        var span = collector.ActivitiesIn(consumer.TraceId).Single(activity => activity.DisplayName == $"{handlerName} handle");
        span.ParentSpanId.ShouldBe(consumer.SpanId);
        span.GetTagItem("messaging.consumer.group.name").ShouldBe(handlerName);
        span.GetTagItem("eksen.handler").ShouldBe(handlerName);
        span.GetTagItem("eksen.event.type").ShouldBe(EventName);
        span.Status.ShouldNotBe(ActivityStatusCode.Error);

        var consumed = collector.MeasurementsOf("messaging.client.consumed.messages").ShouldHaveSingleItem();
        consumed.Tag("outcome").ShouldBe("processed");
        consumed.Tag("eksen.handler").ShouldBe(handlerName);
        collector.MeasurementsOf("messaging.process.duration").ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Duplicate_Delivery_Adds_An_Inbox_Duplicate_Event_And_Counts_It()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TelemetryCollector(EksenTelemetry.EventBus, meterFactory);
        var inboxStore = new Mock<IInboxStore>();
        inboxStore.Setup(s => s.FindAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InboxMessage { Status = InboxMessageStatus.Processed });
        var processor = CreateProcessor<SucceedingHandler>(
            services =>
            {
                services.AddSingleton<SucceedingHandler>();
                services.AddSingleton(inboxStore.Object);
            },
            meterFactory,
            inboxEnabled: true);
        var @event = new TestOrderCreatedEvent();

        using var consumer = TestSource.StartActivity("consumer")!;
        await processor.ProcessAsync(EventName, new JsonEventSerializer().Serialize(@event), null, null, @event.EventId);

        var span = collector.ActivitiesIn(consumer.TraceId).Single(activity => activity.DisplayName.EndsWith(" handle"));
        span.Events.ShouldContain(activityEvent => activityEvent.Name == "inbox.duplicate");
        span.GetTagItem("eksen.outcome").ShouldBe("duplicate");
        collector.MeasurementsOf("messaging.client.consumed.messages").ShouldHaveSingleItem().Tag("outcome").ShouldBe("duplicate");
        collector.MeasurementsOf("eksen.eventbus.inbox.duplicates").ShouldHaveSingleItem().Value.ShouldBe(1);
    }

    [Fact]
    public async Task Retry_Adds_A_Retry_Event_To_The_Handler_Span_And_Counts_It()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TelemetryCollector(EksenTelemetry.EventBus, meterFactory);
        var handler = new FlakyHandler(failures: 1);
        var options = Options.Create(new EksenEventBusOptions
        {
            Inbox = new InboxOptions { IsEnabled = false },
            DeadLetter = new DeadLetterOptions { IsEnabled = false },
            Retry = new RetryOptions { MaxRetryAttempts = 2, InitialDelay = TimeSpan.Zero, MaxDelay = TimeSpan.FromMilliseconds(1) }
        });
        var processor = CreateProcessor<FlakyHandler>(
            services => services.AddSingleton(handler),
            meterFactory,
            retryPipelineProvider: new EventRetryPipelineProvider(options, meterFactory));
        var @event = new TestOrderCreatedEvent();

        using var consumer = TestSource.StartActivity("consumer")!;
        await processor.ProcessAsync(EventName, new JsonEventSerializer().Serialize(@event), null, null, @event.EventId);

        handler.Calls.ShouldBe(2);
        var span = collector.ActivitiesIn(consumer.TraceId).Single(activity => activity.DisplayName.EndsWith(" handle"));
        var retry = span.Events.Single(activityEvent => activityEvent.Name == "retry");
        retry.Tags.ShouldContain(tag => tag.Key == "eksen.retry.attempt" && Equals(tag.Value, 1));
        retry.Tags.ShouldContain(tag => tag.Key == "error.type" && Equals(tag.Value, typeof(InvalidOperationException).FullName));
        var retries = collector.MeasurementsOf("eksen.eventbus.handler.retries").ShouldHaveSingleItem();
        retries.Tag("eksen.handler").ShouldBe(typeof(FlakyHandler).FullName);
        retries.Tag("eksen.event.type").ShouldBe(EventName);
        collector.MeasurementsOf("messaging.client.consumed.messages").ShouldHaveSingleItem().Tag("outcome").ShouldBe("processed");
    }

    [Fact]
    public async Task Dead_Letter_Adds_An_Event_Counts_It_And_Logs_The_Trace_Id()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TelemetryCollector(EksenTelemetry.EventBus, meterFactory);
        var logger = new ListLogger<EventProcessor>();
        var deadLetterStore = new Mock<IDeadLetterStore>();
        var processor = CreateProcessor<FailingHandler>(
            services =>
            {
                services.AddSingleton<FailingHandler>();
                services.AddSingleton(deadLetterStore.Object);
            },
            meterFactory,
            deadLetterEnabled: true,
            logger: logger);
        var @event = new TestOrderCreatedEvent();

        using var consumer = TestSource.StartActivity("consumer")!;
        await processor.ProcessAsync(EventName, new JsonEventSerializer().Serialize(@event), null, null, @event.EventId);

        var span = collector.ActivitiesIn(consumer.TraceId).Single(activity => activity.DisplayName.EndsWith(" handle"));
        span.Events.ShouldContain(activityEvent => activityEvent.Name == "dead_letter");
        span.Events.ShouldContain(activityEvent => activityEvent.Name == "exception");
        span.Status.ShouldBe(ActivityStatusCode.Error);
        collector.MeasurementsOf("messaging.client.consumed.messages").ShouldHaveSingleItem().Tag("outcome").ShouldBe("dead_lettered");
        collector.MeasurementsOf("eksen.eventbus.dead_letters").ShouldHaveSingleItem().Value.ShouldBe(1);
        collector.MeasurementsOf("messaging.process.duration").ShouldHaveSingleItem()
            .Tag("error.type").ShouldBe(typeof(InvalidOperationException).FullName);
        logger.Messages.ShouldContain(message =>
            message.Contains("dead-letter", StringComparison.Ordinal) && message.Contains(consumer.TraceId.ToHexString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Failed_Handler_Without_Dead_Letter_Counts_A_Failure()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TelemetryCollector(EksenTelemetry.EventBus, meterFactory);
        var processor = CreateProcessor<FailingHandler>(services => services.AddSingleton<FailingHandler>(), meterFactory);
        var @event = new TestOrderCreatedEvent();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            processor.ProcessAsync(EventName, new JsonEventSerializer().Serialize(@event), null, null, @event.EventId));

        collector.MeasurementsOf("messaging.client.consumed.messages").ShouldHaveSingleItem().Tag("outcome").ShouldBe("failed");
    }

    [Fact]
    public async Task Store_Gauges_Report_Cached_Backlogs_When_Export_Is_Enabled()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TelemetryCollector(EksenTelemetry.EventBus, meterFactory);
        var outboxStore = new Mock<IOutboxStore>();
        outboxStore.Setup(x => x.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutboxMessageStats { Pending = 3, Processing = 1, Processed = 10, Failed = 2 });
        var deadLetterStore = new Mock<IDeadLetterStore>();
        deadLetterStore.Setup(x => x.GetCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(7);

        var services = new ServiceCollection();
        services.AddSingleton(outboxStore.Object);
        services.AddSingleton(deadLetterStore.Object);
        var provider = services.BuildServiceProvider();
        var gauges = new EventBusStoreGauges(
            provider.GetRequiredService<IServiceScopeFactory>(),
            hasOutbox: true,
            hasInbox: false,
            hasDeadLetters: true,
            exportEnabled: true,
            EventBusTelemetry.For(meterFactory),
            NullLogger.Instance);

        collector.RecordObservableInstruments();
        collector.Measurements.ShouldBeEmpty();

        (await gauges.RefreshAsync(CancellationToken.None)).ShouldBeTrue();
        collector.RecordObservableInstruments();

        var outbox = collector.MeasurementsOf("eksen.eventbus.outbox.messages");
        outbox.Single(measurement => (string?)measurement.Tag("state") == "pending").Value.ShouldBe(3);
        outbox.Single(measurement => (string?)measurement.Tag("state") == "failed").Value.ShouldBe(2);
        collector.MeasurementsOf("eksen.eventbus.dead_letter.messages").ShouldHaveSingleItem().Value.ShouldBe(7);
        collector.MeasurementsOf("eksen.eventbus.inbox.messages").ShouldBeEmpty();
    }

    [Fact]
    public async Task Store_Gauges_Do_Nothing_When_Export_Is_Disabled()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TelemetryCollector(EksenTelemetry.EventBus, meterFactory);
        var outboxStore = new Mock<IOutboxStore>(MockBehavior.Strict);
        var services = new ServiceCollection();
        services.AddSingleton(outboxStore.Object);
        var provider = services.BuildServiceProvider();
        var gauges = new EventBusStoreGauges(
            provider.GetRequiredService<IServiceScopeFactory>(),
            hasOutbox: true,
            hasInbox: false,
            hasDeadLetters: false,
            exportEnabled: false,
            EventBusTelemetry.For(meterFactory),
            NullLogger.Instance);

        await gauges.StartAsync(CancellationToken.None);
        await gauges.StopAsync(CancellationToken.None);
        collector.RecordObservableInstruments();

        collector.Measurements.ShouldBeEmpty();
        outboxStore.VerifyNoOtherCalls();
    }

    [Fact]
    public void AddEventBus_Registers_The_Source_Meter_And_Gauges_With_A_Valid_Container()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEksen(eksen => eksen.AddEventBus());
        services.AddSingleton(new Mock<IEventBusTransport>().Object);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Eksen.Telemetry.Registry.EksenTelemetryRegistry.GetSourceNames(provider).ShouldContain(EksenTelemetry.EventBus);
        Eksen.Telemetry.Registry.EksenTelemetryRegistry.GetMeterNames(provider).ShouldContain(EksenTelemetry.EventBus);
        provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<EventBusStoreGauges>().ShouldHaveSingleItem();
    }

    private static readonly string EventName = EventNameResolver.GetEventName<TestOrderCreatedEvent>();

    private static OutboxProcessorBackgroundService CreateOutboxProcessor(
        IOutboxStore outboxStore,
        IEventBusTransport transport,
        IMeterFactory meterFactory)
    {
        var services = new ServiceCollection();
        services.AddSingleton(outboxStore);
        var provider = services.BuildServiceProvider();
        return new OutboxProcessorBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            transport,
            Options.Create(new EksenEventBusOptions()),
            NullLogger<OutboxProcessorBackgroundService>.Instance,
            meterFactory);
    }

    private static EventProcessor CreateProcessor<THandler>(
        Action<IServiceCollection> configure,
        IMeterFactory meterFactory,
        bool inboxEnabled = false,
        bool deadLetterEnabled = false,
        IEventRetryPipelineProvider? retryPipelineProvider = null,
        ILogger<EventProcessor>? logger = null)
        where THandler : class, IEventHandler<TestOrderCreatedEvent>
    {
        var registry = new EventHandlerRegistry();
        registry.Register<TestOrderCreatedEvent, THandler>();
        var services = new ServiceCollection();
        configure(services);
        var provider = services.BuildServiceProvider();

        if (retryPipelineProvider == null)
        {
            var noRetry = new Mock<IEventRetryPipelineProvider>();
            noRetry.Setup(p => p.GetPipeline()).Returns(ResiliencePipeline.Empty);
            retryPipelineProvider = noRetry.Object;
        }

        return new EventProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            registry,
            retryPipelineProvider,
            Options.Create(new EksenEventBusOptions
            {
                AppName = "TestApp",
                Inbox = new InboxOptions { IsEnabled = inboxEnabled },
                DeadLetter = new DeadLetterOptions { IsEnabled = deadLetterEnabled }
            }),
            logger ?? NullLogger<EventProcessor>.Instance,
            new JsonEventSerializer(),
            meterFactory);
    }

    private sealed class SucceedingHandler : IEventHandler<TestOrderCreatedEvent>
    {
        public Task HandleAsync(TestOrderCreatedEvent @event, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FailingHandler : IEventHandler<TestOrderCreatedEvent>
    {
        public async Task HandleAsync(TestOrderCreatedEvent @event, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new InvalidOperationException("Handler failed");
        }
    }

    private sealed class FlakyHandler(int failures) : IEventHandler<TestOrderCreatedEvent>
    {
        public int Calls { get; private set; }

        public async Task HandleAsync(TestOrderCreatedEvent @event, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            Calls++;
            if (Calls <= failures)
            {
                throw new InvalidOperationException("Transient failure");
            }
        }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (Messages)
            {
                Messages.Add(formatter(state, exception));
            }
        }
    }
}
