using System.Collections.Concurrent;
using System.Text;
using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Shouldly;

namespace Eksen.EventBus.RabbitMq.Tests;

/// <summary>
/// The transport against a real broker: message ids, targeted routing, delayed redelivery, dead-letter queues and
/// replay.
/// </summary>
public class RabbitMqBrokerTests(RabbitMqBrokerFixture broker) : EksenUnitTestBase, IClassFixture<RabbitMqBrokerFixture>
{
    private static readonly string EventName = EventNameResolver.GetEventName<BrokerOrderPlaced>();

    [Fact]
    public async Task Published_Message_Id_Is_The_EventId_And_TargetApp_Reaches_Only_That_App()
    {
        var exchange = UniqueName("exchange");
        await using var billing = await StartAppAsync(exchange, "billing-" + Guid.NewGuid().ToString("N"), bus => bus.Subscribe<BrokerOrderPlaced, RecordingHandler>());
        await using var shipping = await StartAppAsync(exchange, "shipping-" + Guid.NewGuid().ToString("N"), bus => bus.Subscribe<BrokerOrderPlaced, RecordingHandler>());

        // A raw queue bound to the targeted routing key shows what is on the wire.
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        var probe = (await channel.QueueDeclareAsync(exclusive: true, cancellationToken: TestContext.Current.CancellationToken)).QueueName;
        await channel.QueueBindAsync(probe, exchange, $"{shipping.AppName}.{EventName}", cancellationToken: TestContext.Current.CancellationToken);

        var targeted = new BrokerOrderPlaced { OrderNumber = "targeted" };
        await PublishAsync(billing, targeted, new PublishOptions { TargetApp = shipping.AppName });
        var broadcast = new BrokerOrderPlaced { OrderNumber = "broadcast" };
        await PublishAsync(billing, broadcast, options: null);

        await WaitUntilAsync(() => shipping.State.EventIds.Count == 2 && billing.State.EventIds.Count == 1);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        shipping.State.EventIds.ShouldBe([targeted.EventId, broadcast.EventId], ignoreOrder: true);
        billing.State.EventIds.ShouldBe([broadcast.EventId]);

        var wire = await channel.BasicGetAsync(probe, autoAck: true, cancellationToken: TestContext.Current.CancellationToken);
        wire.ShouldNotBeNull();
        wire.BasicProperties.MessageId.ShouldBe(targeted.EventId.ToString());
        wire.BasicProperties.DeliveryMode.ShouldBe(DeliveryModes.Persistent);
    }

    [Fact]
    public async Task Unroutable_Publish_Is_Returned_By_The_Broker()
    {
        await using var app = await StartAppAsync(UniqueName("exchange"), UniqueName("app"), _ => { });

        await Should.ThrowAsync<PublishException>(() => app.Transport.PublishAsync(
            "Nobody.Listens", "{}", null, null, null, Guid.NewGuid(), null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Failed_Delivery_Is_Redelivered_With_Backoff_To_The_Failing_Handler_Queue_Only()
    {
        await using var app = await StartAppAsync(
            UniqueName("exchange"),
            UniqueName("app"),
            bus => bus
                .Subscribe<BrokerOrderPlaced, RecordingHandler>()
                .Subscribe<BrokerOrderPlaced, FlakyHandler>(),
            maxRedeliveries: 3);
        app.State.FlakyFailures = 2;
        var placed = new BrokerOrderPlaced { OrderNumber = "flaky" };

        await PublishAsync(app, placed, options: null);

        await WaitUntilAsync(() => app.State.FlakyRuns.Count == 3);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        app.State.EventIds.ShouldBe([placed.EventId]);
        app.State.FlakyRuns.Count.ShouldBe(3, "each redelivery reaches the failing queue exactly once");
        var flakyAttempts = app.Deliveries.Where(delivery => delivery.HandlerTypeName == typeof(FlakyHandler).FullName).ToList();
        flakyAttempts.Select(delivery => delivery.Attempt).ShouldBe([1, 2, 3]);
        flakyAttempts.ShouldAllBe(delivery => delivery.EventId == placed.EventId && !delivery.IsFinalAttempt);
        flakyAttempts[1].FirstFailureTime.ShouldNotBeNull();
        app.Deliveries.Count(delivery => delivery.HandlerTypeName == typeof(RecordingHandler).FullName).ShouldBe(1);

        // Redelivery n waits 300ms * 2^(n-1), +/- 20%.
        var runs = app.State.FlakyRuns.ToArray();
        (runs[1] - runs[0]).TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(240 - 20);
        (runs[2] - runs[1]).TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(480 - 20);
    }

    [Fact]
    public async Task Exhausted_Attempts_Land_In_The_Dead_Letter_Queue_With_Failure_Headers_And_Replay()
    {
        await using var app = await StartAppAsync(
            UniqueName("exchange"),
            UniqueName("app"),
            bus => bus.Subscribe<BrokerOrderPlaced, FlakyHandler>(),
            maxRedeliveries: 1);
        app.State.FlakyFailures = int.MaxValue;
        var placed = new BrokerOrderPlaced { OrderNumber = "doomed" };

        await PublishAsync(app, placed, options: null);

        var replayer = app.Services.GetRequiredService<IDeadLetterQueueReplayer>();
        var queue = await WaitForDeadLetterAsync(replayer, 1);
        queue.HandlerQueueName.ShouldBe($"{app.AppName}.{typeof(FlakyHandler).FullName}");
        queue.DeadLetterQueueName.ShouldBe(queue.HandlerQueueName + ".dlq");
        app.State.FlakyRuns.Count.ShouldBe(2);

        var dead = (await replayer.PeekAsync(queue.HandlerQueueName, cancellationToken: TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
        dead.MessageId.ShouldBe(placed.EventId.ToString());
        dead.Reason.ShouldBe(RabbitMqDeadLetterReasons.MaxAttemptsExceeded);
        dead.ExceptionType.ShouldBe(typeof(InvalidOperationException).FullName);
        dead.ExceptionMessage.ShouldBe("flaky handler failed");
        dead.Attempts.ShouldBe(2);
        dead.OriginalRoutingKey.ShouldBe(EventName);
        dead.FirstFailureTime.ShouldNotBeNull();
        dead.FailedTime.ShouldNotBeNull();
        dead.FailedTime!.Value.ShouldBeGreaterThanOrEqualTo(dead.FirstFailureTime!.Value);
        (await replayer.GetQueuesAsync(TestContext.Current.CancellationToken)).Single().MessageCount.ShouldBe(1u, "peeking leaves the message in place");

        app.State.FlakyFailures = 0;
        (await replayer.ReplayAsync(queue.HandlerQueueName, cancellationToken: TestContext.Current.CancellationToken)).ShouldBe(1);

        await WaitUntilAsync(() => app.State.FlakyRuns.Count == 3);
        var replayed = app.Deliveries.Last();
        replayed.Attempt.ShouldBe(1, "a replayed message starts a fresh retry budget");
        (await replayer.GetQueuesAsync(TestContext.Current.CancellationToken)).Single().MessageCount.ShouldBe(0u);
    }

    [Fact]
    public async Task Poison_Message_Skips_Retries_And_Goes_Straight_To_The_Dead_Letter_Queue()
    {
        await using var app = await StartAppAsync(
            UniqueName("exchange"),
            UniqueName("app"),
            bus => bus.Subscribe<BrokerOrderPlaced, FlakyHandler>(),
            maxRedeliveries: 3);

        await app.Transport.PublishAsync(EventName, "{not json", null, null, null, Guid.NewGuid(), null, TestContext.Current.CancellationToken);

        var replayer = app.Services.GetRequiredService<IDeadLetterQueueReplayer>();
        var queue = await WaitForDeadLetterAsync(replayer, 1);
        var dead = (await replayer.PeekAsync(queue.HandlerQueueName, cancellationToken: TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
        dead.Reason.ShouldBe(RabbitMqDeadLetterReasons.Poison);
        dead.ExceptionType.ShouldBe(typeof(PoisonMessageException).FullName);
        dead.Attempts.ShouldBe(1);
        dead.Payload.ShouldBe("{not json");
        app.State.FlakyRuns.ShouldBeEmpty();
        app.Deliveries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Message_Without_Event_Id_Is_Poison()
    {
        await using var app = await StartAppAsync(
            UniqueName("exchange"),
            UniqueName("app"),
            bus => bus.Subscribe<BrokerOrderPlaced, FlakyHandler>());

        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        await channel.BasicPublishAsync(app.Exchange, EventName, mandatory: false, new BasicProperties(), Encoding.UTF8.GetBytes("{}"), cancellationToken: TestContext.Current.CancellationToken);

        var replayer = app.Services.GetRequiredService<IDeadLetterQueueReplayer>();
        var queue = await WaitForDeadLetterAsync(replayer, 1);
        (await replayer.PeekAsync(queue.HandlerQueueName, cancellationToken: TestContext.Current.CancellationToken))
            .ShouldHaveSingleItem().Reason.ShouldBe(RabbitMqDeadLetterReasons.Poison);
        app.Deliveries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rejected_Message_Is_Routed_To_The_Queues_Own_Dead_Letter_Queue()
    {
        await using var app = await StartAppAsync(
            UniqueName("exchange"),
            UniqueName("app"),
            bus => bus.Subscribe<BrokerOrderPlaced, FlakyHandler>());
        await app.Transport.StopListeningAsync(TestContext.Current.CancellationToken);
        var queueName = $"{app.AppName}.{typeof(FlakyHandler).FullName}";
        await PublishAsync(app, new BrokerOrderPlaced { OrderNumber = "rejected" }, options: null);

        // A message the broker dead-letters by itself (a reject here) follows the queue's dead-letter arguments.
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        BasicGetResult? message = null;
        await WaitUntilAsync(async () => (message = await channel.BasicGetAsync(queueName, autoAck: false)) != null);
        await channel.BasicRejectAsync(message!.DeliveryTag, requeue: false, cancellationToken: TestContext.Current.CancellationToken);

        var replayer = app.Services.GetRequiredService<IDeadLetterQueueReplayer>();
        var queue = await WaitForDeadLetterAsync(replayer, 1);
        (await replayer.PeekAsync(queue.HandlerQueueName, cancellationToken: TestContext.Current.CancellationToken))
            .ShouldHaveSingleItem().Reason.ShouldBe("rejected");
    }

    private async Task<DeadLetterQueueInfo> WaitForDeadLetterAsync(IDeadLetterQueueReplayer replayer, uint count)
    {
        DeadLetterQueueInfo? queue = null;
        await WaitUntilAsync(async () =>
        {
            queue = (await replayer.GetQueuesAsync()).FirstOrDefault(info => info.MessageCount == count);
            return queue != null;
        });
        return queue!;
    }

    private static async Task PublishAsync(BrokerApp app, BrokerOrderPlaced @event, PublishOptions? options)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IEventBus>().PublishAsync(@event, options, TestContext.Current.CancellationToken);
    }

    private async Task<IConnection> CreateConnectionAsync()
    {
        return await new ConnectionFactory { HostName = broker.HostName, Port = broker.Port }.CreateConnectionAsync();
    }

    private async Task<BrokerApp> StartAppAsync(
        string exchange,
        string appName,
        Action<IEksenEventBusBuilder> subscribe,
        int maxRedeliveries = 2)
    {
        var state = new BrokerState();
        var deliveries = new ConcurrentQueue<EventDelivery>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(state);
        services.AddEksen(eksen => eksen.AddEventBus(bus =>
        {
            bus.Configure(options =>
            {
                options.AppName = appName;
                options.Outbox.IsEnabled = false;
                options.Retry.MaxRetryAttempts = 0;
                options.DeadLetter.MaxRetryAttemptsBeforeDeadLetter = maxRedeliveries;
                options.Redelivery.InitialDelay = TimeSpan.FromMilliseconds(300);
                options.Redelivery.BackoffMultiplier = 2;
                options.Redelivery.MaxDelay = TimeSpan.FromSeconds(5);
            });
            subscribe(bus);
            bus.UseRabbitMq(rabbit => rabbit.Configure(options =>
            {
                options.HostName = broker.HostName;
                options.Port = broker.Port;
                options.ExchangeName = exchange;
                options.DeadLetterExchangeName = exchange + ".dlx";
            }));
        }));
        services.AddSingleton<EventProcessor>();
        services.Replace(ServiceDescriptor.Singleton<IEventProcessor>(provider =>
            new RecordingProcessor(provider.GetRequiredService<EventProcessor>(), deliveries)));

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var transport = provider.GetRequiredService<RabbitMqEventBusTransport>();
        await transport.StartListeningAsync(TestContext.Current.CancellationToken);
        return new BrokerApp(appName, exchange, provider, transport, state, deliveries);
    }

    private static string UniqueName(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        await WaitUntilAsync(() => Task.FromResult(condition()));
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!await condition())
        {
            await Task.Delay(50, timeout.Token);
        }
    }

    public sealed class BrokerOrderPlaced : IntegrationEvent
    {
        public string OrderNumber { get; init; } = "";
    }

    public sealed class BrokerState
    {
        public ConcurrentQueue<Guid> EventIds { get; } = new();

        public ConcurrentQueue<DateTime> FlakyRuns { get; } = new();

        public int FlakyFailures { get; set; }
    }

    public sealed class RecordingHandler(BrokerState state) : IEventHandler<BrokerOrderPlaced>
    {
        public Task HandleAsync(BrokerOrderPlaced @event, CancellationToken cancellationToken = default)
        {
            state.EventIds.Enqueue(@event.EventId);
            return Task.CompletedTask;
        }
    }

    public sealed class FlakyHandler(BrokerState state) : IEventHandler<BrokerOrderPlaced>
    {
        public Task HandleAsync(BrokerOrderPlaced @event, CancellationToken cancellationToken = default)
        {
            state.FlakyRuns.Enqueue(DateTime.UtcNow);
            if (state.FlakyRuns.Count <= state.FlakyFailures)
            {
                throw new InvalidOperationException("flaky handler failed");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProcessor(IEventProcessor inner, ConcurrentQueue<EventDelivery> deliveries) : IEventProcessor
    {
        public Task ProcessAsync(
            string eventTypeName,
            string payload,
            string? correlationId,
            string? sourceApp,
            Guid eventId,
            CancellationToken cancellationToken = default)
        {
            return inner.ProcessAsync(eventTypeName, payload, correlationId, sourceApp, eventId, cancellationToken);
        }

        public Task<EventProcessingOutcome> ProcessAsync(EventDelivery delivery, CancellationToken cancellationToken = default)
        {
            deliveries.Enqueue(delivery);
            return inner.ProcessAsync(delivery, cancellationToken);
        }
    }

    private sealed class BrokerApp(
        string appName,
        string exchange,
        ServiceProvider services,
        RabbitMqEventBusTransport transport,
        BrokerState state,
        ConcurrentQueue<EventDelivery> deliveries
    ) : IAsyncDisposable
    {
        public string AppName { get; } = appName;

        public string Exchange { get; } = exchange;

        public ServiceProvider Services { get; } = services;

        public RabbitMqEventBusTransport Transport { get; } = transport;

        public BrokerState State { get; } = state;

        public List<EventDelivery> Deliveries
        {
            get { return [.. deliveries]; }
        }

        public async ValueTask DisposeAsync()
        {
            await Transport.StopListeningAsync();
            await Services.DisposeAsync();
        }
    }
}
