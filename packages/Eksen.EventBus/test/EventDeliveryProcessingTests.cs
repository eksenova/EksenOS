using System.Collections.Concurrent;
using System.Diagnostics;
using Eksen.EventBus.BackgroundServices;
using Eksen.EventBus.DeadLetter;
using Eksen.EventBus.Inbox;
using Eksen.EventBus.Outbox;
using Eksen.EventBus.Retry;
using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Polly;
using Shouldly;

namespace Eksen.EventBus.Tests;

public class EventDeliveryProcessingTests : EksenUnitTestBase
{
    private static readonly string EventName = EventNameResolver.GetEventName<TestOrderCreatedEvent>();

    [Fact]
    public async Task NonFinal_Failure_Rethrows_Without_Dead_Lettering()
    {
        var deadLetterStore = new Mock<IDeadLetterStore>();
        var processor = CreateProcessor<CountingHandler>(services => services.AddSingleton(deadLetterStore.Object), out var state);
        state.FailuresBeforeSuccess = int.MaxValue;

        await Should.ThrowAsync<InvalidOperationException>(() =>
            processor.ProcessAsync(CreateDelivery<CountingHandler>(isFinalAttempt: false)));

        deadLetterStore.Verify(x => x.SaveAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Final_Failure_Is_Dead_Lettered_With_Its_Handler()
    {
        DeadLetterMessage? saved = null;
        var deadLetterStore = new Mock<IDeadLetterStore>();
        deadLetterStore.Setup(x => x.SaveAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()))
            .Callback<DeadLetterMessage, CancellationToken>((message, _) => saved = message)
            .Returns(Task.CompletedTask);
        var processor = CreateProcessor<CountingHandler>(services => services.AddSingleton(deadLetterStore.Object), out var state);
        state.FailuresBeforeSuccess = int.MaxValue;
        var delivery = CreateDelivery<CountingHandler>(isFinalAttempt: true, attempt: 3);

        var outcome = await processor.ProcessAsync(delivery, cancellationToken: TestContext.Current.CancellationToken);

        outcome.ShouldBe(EventProcessingOutcome.DeadLettered);
        saved.ShouldNotBeNull();
        saved.OriginalMessageId.ShouldBe(delivery.EventId);
        saved.HandlerType.ShouldBe(typeof(CountingHandler).FullName);
    }

    [Fact]
    public async Task Poison_Payload_Skips_Retries_And_Is_Dead_Lettered_Even_Before_The_Final_Attempt()
    {
        var deadLetterStore = new Mock<IDeadLetterStore>();
        var processor = CreateProcessor<CountingHandler>(
            services => services.AddSingleton(deadLetterStore.Object),
            out var state,
            retry: new RetryOptions { MaxRetryAttempts = 3, InitialDelay = TimeSpan.FromMilliseconds(1) });

        var outcome = await processor.ProcessAsync(CreateDelivery<CountingHandler>(isFinalAttempt: false, payload: "{not json"), cancellationToken: TestContext.Current.CancellationToken);

        outcome.ShouldBe(EventProcessingOutcome.DeadLettered);
        state.Calls.ShouldBe(0);
        deadLetterStore.Verify(x => x.SaveAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Poison_Without_A_Dead_Letter_Store_Throws_PoisonMessageException()
    {
        var processor = CreateProcessor<CountingHandler>(_ => { }, out _);

        await Should.ThrowAsync<PoisonMessageException>(() =>
            processor.ProcessAsync(CreateDelivery<CountingHandler>(isFinalAttempt: false, payload: "{not json")));
    }

    [Fact]
    public async Task Unknown_Handler_Is_Poison()
    {
        var processor = CreateProcessor<CountingHandler>(_ => { }, out _);
        var delivery = new EventDelivery
        {
            EventTypeName = EventName,
            Payload = "{}",
            EventId = Guid.NewGuid(),
            HandlerTypeName = "Missing.Handler",
            IsFinalAttempt = false
        };

        await Should.ThrowAsync<PoisonMessageException>(() => processor.ProcessAsync(delivery));
    }

    [Fact]
    public async Task Without_Unit_Of_Work_The_Inbox_Deduplicates_The_Same_EventId()
    {
        var inbox = new ClaimingInboxStore();
        var processor = CreateProcessor<CountingHandler>(services => services.AddSingleton<IInboxStore>(inbox), out var state);
        var delivery = CreateDelivery<CountingHandler>(isFinalAttempt: true);

        (await processor.ProcessAsync(delivery, cancellationToken: TestContext.Current.CancellationToken)).ShouldBe(EventProcessingOutcome.Processed);
        (await processor.ProcessAsync(delivery, cancellationToken: TestContext.Current.CancellationToken)).ShouldBe(EventProcessingOutcome.Duplicate);

        state.Calls.ShouldBe(1);
        inbox.Completed.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Without_Unit_Of_Work_A_Failed_Handler_Releases_Its_Claim()
    {
        var inbox = new ClaimingInboxStore();
        var processor = CreateProcessor<CountingHandler>(services => services.AddSingleton<IInboxStore>(inbox), out var state);
        state.FailuresBeforeSuccess = 1;
        var delivery = CreateDelivery<CountingHandler>(isFinalAttempt: false);

        await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessAsync(delivery));
        inbox.Released.ShouldHaveSingleItem();

        (await processor.ProcessAsync(delivery, cancellationToken: TestContext.Current.CancellationToken)).ShouldBe(EventProcessingOutcome.Processed);
        state.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task A_Claim_Held_By_Another_Consumer_Is_Retried_Not_Acknowledged()
    {
        var inbox = new ClaimingInboxStore();
        var delivery = CreateDelivery<CountingHandler>(isFinalAttempt: false);
        await inbox.TryClaimAsync(new InboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = delivery.EventId,
            EventType = EventName,
            HandlerType = delivery.HandlerTypeName,
            Payload = delivery.Payload,
            Status = InboxMessageStatus.Processing
        }, cancellationToken: TestContext.Current.CancellationToken);
        var processor = CreateProcessor<CountingHandler>(services => services.AddSingleton<IInboxStore>(inbox), out var state);

        await Should.ThrowAsync<InboxClaimConflictException>(() => processor.ProcessAsync(delivery));
        state.Calls.ShouldBe(0);
    }

    [Fact]
    public void Retry_Pipeline_Does_Not_Retry_Poison_Messages()
    {
        EventRetryPipelineProvider.IsRetryable(new PoisonMessageException("poison")).ShouldBeFalse();
        EventRetryPipelineProvider.IsRetryable(new OperationCanceledException()).ShouldBeFalse();
        EventRetryPipelineProvider.IsRetryable(new InvalidOperationException()).ShouldBeTrue();
    }

    [Fact]
    public async Task Retry_Pipeline_Uses_The_Backoff_Multiplier()
    {
        var options = new EksenEventBusOptions
        {
            Retry = new RetryOptions
            {
                MaxRetryAttempts = 2,
                InitialDelay = TimeSpan.FromMilliseconds(60),
                MaxDelay = TimeSpan.FromSeconds(5),
                BackoffMultiplier = 5
            }
        };
        var pipeline = new EventRetryPipelineProvider(Options.Create(options)).GetPipeline();
        var stopwatch = Stopwatch.StartNew();
        var attempts = 0;

        await Should.ThrowAsync<InvalidOperationException>(async () => await pipeline.ExecuteAsync(_ =>
        {
            attempts++;
            throw new InvalidOperationException("fails");
        }));

        attempts.ShouldBe(3);
        // 60ms then 300ms, each at least 80% of nominal; a doubling backoff would wait at most 216ms in total.
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(280);
    }

    [Theory]
    [InlineData(1, 1000)]
    [InlineData(2, 3000)]
    [InlineData(3, 9000)]
    [InlineData(4, 20000)]
    public void Exponential_Backoff_Applies_Multiplier_Cap_And_Jitter(int attempt, int nominalMilliseconds)
    {
        var initial = TimeSpan.FromSeconds(1);
        var max = TimeSpan.FromSeconds(20);

        ExponentialBackoff.GetNominalDelay(attempt, initial, max, 3).TotalMilliseconds.ShouldBe(nominalMilliseconds);
        for (var i = 0; i < 50; i++)
        {
            var delay = ExponentialBackoff.GetDelay(attempt, initial, max, 3, 0.2).TotalMilliseconds;
            delay.ShouldBeGreaterThanOrEqualTo(nominalMilliseconds * 0.8 - 0.001);
            delay.ShouldBeLessThanOrEqualTo(Math.Min(nominalMilliseconds * 1.2, max.TotalMilliseconds) + 0.001);
        }
    }

    [Fact]
    public async Task Cleanup_Purges_Processed_Outbox_And_Inbox_Rows_By_Their_Windows()
    {
        var now = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var outbox = new Mock<IOutboxStore>();
        outbox.SetupSequence(x => x.DeleteProcessedAsync(It.IsAny<DateTime>(), 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2)
            .ReturnsAsync(1);
        var inbox = new Mock<IInboxStore>();
        inbox.Setup(x => x.DeleteProcessedAsync(It.IsAny<DateTime>(), 2, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        var services = new ServiceCollection();
        services.AddSingleton(outbox.Object);
        services.AddSingleton(inbox.Object);
        var options = new EksenEventBusOptions
        {
            Outbox = new OutboxOptions { ProcessedRetention = TimeSpan.FromDays(3) },
            Inbox = new InboxOptions { IdempotencyWindow = TimeSpan.FromDays(1) },
            Cleanup = new CleanupOptions { BatchSize = 2 }
        };
        var cleanup = new EventBusCleanupBackgroundService(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            NullLogger<EventBusCleanupBackgroundService>.Instance);

        var (outboxDeleted, inboxDeleted) = await cleanup.CleanupAsync(now, CancellationToken.None);

        outboxDeleted.ShouldBe(3);
        inboxDeleted.ShouldBe(0);
        outbox.Verify(x => x.DeleteProcessedAsync(now.AddDays(-3), 2, It.IsAny<CancellationToken>()), Times.Exactly(2));
        inbox.Verify(x => x.DeleteProcessedAsync(now.AddDays(-1), 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cleanup_And_Outbox_Processor_Need_No_Stores()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var options = Options.Create(new EksenEventBusOptions());
        var cleanup = new EventBusCleanupBackgroundService(scopeFactory, options, NullLogger<EventBusCleanupBackgroundService>.Instance);
        var outboxProcessor = new OutboxProcessorBackgroundService(
            scopeFactory,
            new Mock<IEventBusTransport>().Object,
            options,
            NullLogger<OutboxProcessorBackgroundService>.Instance);

        (await cleanup.CleanupAsync(DateTime.UtcNow, CancellationToken.None)).ShouldBe((0, 0));
        await outboxProcessor.StartAsync(CancellationToken.None);
        var completed = await Task.WhenAny(outboxProcessor.ExecuteTask!, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken));
        completed.ShouldBe(outboxProcessor.ExecuteTask);
        await outboxProcessor.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Publishing_Without_An_Outbox_Store_Goes_Straight_To_The_Transport()
    {
        var transport = new Mock<IEventBusTransport>();
        var provider = new ServiceCollection().BuildServiceProvider();
        var bus = new DefaultEventBus(
            transport.Object,
            new JsonEventSerializer(),
            Options.Create(new EksenEventBusOptions { AppName = "consumer" }),
            provider.GetRequiredService<IServiceScopeFactory>(),
            new Mock<ILocalEventDispatcher>().Object);
        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1" };

        await bus.PublishAsync(@event, cancellationToken: TestContext.Current.CancellationToken);

        transport.Verify(x => x.PublishAsync(
            EventName, It.IsAny<string>(), It.IsAny<string?>(), "consumer", null, @event.EventId,
            It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Outbox_Row_Carries_The_Event_Id()
    {
        OutboxMessage? saved = null;
        var outbox = new Mock<IOutboxStore>();
        outbox.Setup(x => x.SaveAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, CancellationToken>((message, _) => saved = message)
            .Returns(Task.CompletedTask);
        var services = new ServiceCollection();
        services.AddSingleton(outbox.Object);
        var bus = new DefaultEventBus(
            new Mock<IEventBusTransport>().Object,
            new JsonEventSerializer(),
            Options.Create(new EksenEventBusOptions()),
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new Mock<ILocalEventDispatcher>().Object);
        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1" };

        await bus.PublishAsync(@event, cancellationToken: TestContext.Current.CancellationToken);

        saved.ShouldNotBeNull();
        saved.EventId.ShouldBe(@event.EventId);
        saved.Id.ShouldNotBe(@event.EventId);
    }

    private static EventDelivery CreateDelivery<THandler>(
        bool isFinalAttempt,
        int attempt = 1,
        string? payload = null)
    {
        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 10 };
        return new EventDelivery
        {
            EventTypeName = EventName,
            Payload = payload ?? new JsonEventSerializer().Serialize(@event),
            EventId = @event.EventId,
            HandlerTypeName = typeof(THandler).FullName!,
            Attempt = attempt,
            IsFinalAttempt = isFinalAttempt
        };
    }

    private static EventProcessor CreateProcessor<THandler>(
        Action<IServiceCollection> configure,
        out HandlerState state,
        RetryOptions? retry = null)
        where THandler : class, IEventHandler<TestOrderCreatedEvent>
    {
        state = new HandlerState();
        var registry = new EventHandlerRegistry();
        registry.Register<TestOrderCreatedEvent, THandler>();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddScoped<THandler>();
        configure(services);
        var options = Options.Create(new EksenEventBusOptions
        {
            Retry = retry ?? new RetryOptions { MaxRetryAttempts = 0 }
        });

        return new EventProcessor(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            registry,
            retry == null ? Mock.Of<IEventRetryPipelineProvider>(p => p.GetPipeline() == ResiliencePipeline.Empty) : new EventRetryPipelineProvider(options),
            options,
            NullLogger<EventProcessor>.Instance);
    }

    public sealed class HandlerState
    {
        private int _calls;

        public int FailuresBeforeSuccess { get; set; }

        public int Calls
        {
            get { return _calls; }
        }

        public int Next()
        {
            return Interlocked.Increment(ref _calls);
        }
    }

    public sealed class CountingHandler(HandlerState state) : IEventHandler<TestOrderCreatedEvent>
    {
        public Task HandleAsync(TestOrderCreatedEvent @event, CancellationToken cancellationToken = default)
        {
            if (state.Next() <= state.FailuresBeforeSuccess)
            {
                throw new InvalidOperationException("handler failed");
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A non-transactional inbox with an atomic claim, like the Redis one.
    /// </summary>
    private sealed class ClaimingInboxStore : IInboxStore
    {
        private readonly ConcurrentDictionary<(Guid, string), InboxMessage> _messages = new();

        public List<Guid> Completed { get; } = [];

        public List<Guid> Released { get; } = [];

        public bool EnlistsInUnitOfWork
        {
            get { return false; }
        }

        public Task<InboxClaim> TryClaimAsync(InboxMessage message, CancellationToken cancellationToken = default)
        {
            var current = _messages.GetOrAdd((message.EventId, message.HandlerType), message);
            if (ReferenceEquals(current, message))
            {
                return Task.FromResult(new InboxClaim(InboxClaimStatus.Claimed, message));
            }

            return Task.FromResult(new InboxClaim(
                current.Status == InboxMessageStatus.Processed ? InboxClaimStatus.AlreadyProcessed : InboxClaimStatus.InProgress,
                current));
        }

        public Task CompleteAsync(InboxMessage message, CancellationToken cancellationToken = default)
        {
            message.Status = InboxMessageStatus.Processed;
            Completed.Add(message.EventId);
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(InboxMessage message, string error, CancellationToken cancellationToken = default)
        {
            _messages.TryRemove((message.EventId, message.HandlerType), out _);
            Released.Add(message.EventId);
            return Task.CompletedTask;
        }

        public Task<InboxMessage?> FindAsync(Guid eventId, string handlerType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_messages.GetValueOrDefault((eventId, handlerType)));
        }

        public Task<bool> ExistsAsync(Guid eventId, string handlerType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_messages.ContainsKey((eventId, handlerType)));
        }

        public Task<int> DeleteProcessedAsync(DateTime processedBefore, int batchSize, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task SaveAsync(InboxMessage message, CancellationToken cancellationToken = default)
        {
            _messages[(message.EventId, message.HandlerType)] = message;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<InboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<InboxMessage>>([]);
        }

        public Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task IncrementRetryCountAsync(Guid messageId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<InboxMessageStats> GetStatsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new InboxMessageStats());
        }

        public Task<IReadOnlyList<InboxMessage>> GetMessagesAsync(
            InboxMessageStatus? status = null,
            int skip = 0,
            int take = 50,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<InboxMessage>>([.. _messages.Values]);
        }
    }
}
