using Eksen.EventBus.DeadLetter;
using Eksen.EventBus.Inbox;
using Eksen.EventBus.Retry;
using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Polly;
using Shouldly;

namespace Eksen.EventBus.Tests;

public class EventProcessorTests : EksenUnitTestBase
{
    private class TestHandler : IEventHandler<TestOrderCreatedEvent>
    {
        public bool WasCalled { get; private set; }

        public Task HandleAsync(TestOrderCreatedEvent @event, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private class FailingHandler : IEventHandler<TestOrderCreatedEvent>
    {
        public Task HandleAsync(TestOrderCreatedEvent @event, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Handler failed");
        }
    }

    private static EventHandlerRegistry CreateRegistry<THandler>()
        where THandler : class, IEventHandler<TestOrderCreatedEvent>
    {
        var registry = new EventHandlerRegistry();
        registry.Register<TestOrderCreatedEvent, THandler>();
        return registry;
    }

    private static IEventRetryPipelineProvider CreateNoRetryPipeline()
    {
        var mock = new Mock<IEventRetryPipelineProvider>();
        mock.Setup(p => p.GetPipeline())
            .Returns(ResiliencePipeline.Empty);
        return mock.Object;
    }

    private static EksenEventBusOptions DefaultOptions() => new()
    {
        AppName = "TestApp",
        Inbox = new InboxOptions { IsEnabled = false },
        DeadLetter = new DeadLetterOptions { IsEnabled = false }
    };

    private static string SerializeEvent(TestOrderCreatedEvent @event)
    {
        var serializer = new JsonEventSerializer();
        return serializer.Serialize(@event);
    }

    [Fact]
    public async Task ProcessAsync_Should_Do_Nothing_When_No_Handlers_Registered()
    {
        // Arrange
        var registry = new EventHandlerRegistry();
        var services = new ServiceCollection();
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var processor = new EventProcessor(
            scopeFactory,
            registry,
            CreateNoRetryPipeline(),
            Options.Create(DefaultOptions()),
            NullLogger<EventProcessor>.Instance);

        // Act & Assert (should not throw)
        await processor.ProcessAsync(
            "NonExistent.Event",
            "{}",
            null,
            null,
            Guid.NewGuid());
    }

    [Fact]
    public async Task ProcessAsync_Should_Invoke_Handler()
    {
        // Arrange
        var handler = new TestHandler();
        var registry = CreateRegistry<TestHandler>();

        var services = new ServiceCollection();
        services.AddSingleton<TestHandler>(handler);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var processor = new EventProcessor(
            scopeFactory,
            registry,
            CreateNoRetryPipeline(),
            Options.Create(DefaultOptions()),
            NullLogger<EventProcessor>.Instance);

        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };
        var payload = SerializeEvent(@event);
        var eventName = EventNameResolver.GetEventName<TestOrderCreatedEvent>();

        // Act
        await processor.ProcessAsync(eventName, payload, null, null, @event.EventId);

        // Assert
        handler.WasCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessAsync_Should_Check_Inbox_Idempotency_When_Enabled()
    {
        // Arrange
        var handler = new TestHandler();
        var registry = CreateRegistry<TestHandler>();

        var inboxStore = new Mock<IInboxStore>();
        inboxStore
            .Setup(s => s.ExistsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var services = new ServiceCollection();
        services.AddSingleton<TestHandler>(handler);
        services.AddSingleton(inboxStore.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var options = new EksenEventBusOptions
        {
            AppName = "TestApp",
            Inbox = new InboxOptions { IsEnabled = true },
            DeadLetter = new DeadLetterOptions { IsEnabled = false }
        };

        var processor = new EventProcessor(
            scopeFactory,
            registry,
            CreateNoRetryPipeline(),
            Options.Create(options),
            NullLogger<EventProcessor>.Instance);

        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };
        var payload = SerializeEvent(@event);
        var eventName = EventNameResolver.GetEventName<TestOrderCreatedEvent>();

        // Act
        await processor.ProcessAsync(eventName, payload, null, null, @event.EventId);

        // Assert (handler should NOT be called because event already processed)
        handler.WasCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessAsync_Should_Save_Inbox_Message_When_Not_Duplicate()
    {
        // Arrange
        var handler = new TestHandler();
        var registry = CreateRegistry<TestHandler>();

        var inboxStore = new Mock<IInboxStore>();
        inboxStore
            .Setup(s => s.ExistsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        inboxStore
            .Setup(s => s.GetMessagesAsync(It.IsAny<InboxMessageStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var services = new ServiceCollection();
        services.AddSingleton<TestHandler>(handler);
        services.AddSingleton(inboxStore.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var options = new EksenEventBusOptions
        {
            AppName = "TestApp",
            Inbox = new InboxOptions { IsEnabled = true },
            DeadLetter = new DeadLetterOptions { IsEnabled = false }
        };

        var processor = new EventProcessor(
            scopeFactory,
            registry,
            CreateNoRetryPipeline(),
            Options.Create(options),
            NullLogger<EventProcessor>.Instance);

        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };
        var payload = SerializeEvent(@event);
        var eventName = EventNameResolver.GetEventName<TestOrderCreatedEvent>();

        // Act
        await processor.ProcessAsync(eventName, payload, "corr-1", "SourceApp", @event.EventId);

        // Assert
        inboxStore.Verify(
            s => s.SaveAsync(It.Is<InboxMessage>(m =>
                m.EventId == @event.EventId &&
                m.EventType == eventName &&
                m.Status == InboxMessageStatus.Processing &&
                m.CorrelationId == "corr-1" &&
                m.SourceApp == "SourceApp"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_Should_Send_To_DeadLetter_When_Handler_Fails()
    {
        // Arrange
        var registry = CreateRegistry<FailingHandler>();

        var deadLetterStore = new Mock<IDeadLetterStore>();
        var services = new ServiceCollection();
        services.AddSingleton<FailingHandler>();
        services.AddSingleton(deadLetterStore.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var options = new EksenEventBusOptions
        {
            AppName = "TestApp",
            Inbox = new InboxOptions { IsEnabled = false },
            DeadLetter = new DeadLetterOptions { IsEnabled = true }
        };

        var processor = new EventProcessor(
            scopeFactory,
            registry,
            CreateNoRetryPipeline(),
            Options.Create(options),
            NullLogger<EventProcessor>.Instance);

        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };
        var payload = SerializeEvent(@event);
        var eventName = EventNameResolver.GetEventName<TestOrderCreatedEvent>();

        // Act
        await processor.ProcessAsync(eventName, payload, "corr-1", "SourceApp", @event.EventId);

        // Assert
        deadLetterStore.Verify(
            s => s.SaveAsync(It.Is<DeadLetterMessage>(m =>
                m.OriginalMessageId == @event.EventId &&
                m.EventType == eventName), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_Should_Notify_On_DeadLetter()
    {
        // Arrange
        var registry = CreateRegistry<FailingHandler>();

        var deadLetterStore = new Mock<IDeadLetterStore>();
        var notifier = new Mock<IDeadLetterNotifier>();

        var services = new ServiceCollection();
        services.AddSingleton<FailingHandler>();
        services.AddSingleton(deadLetterStore.Object);
        services.AddSingleton(notifier.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var options = new EksenEventBusOptions
        {
            AppName = "TestApp",
            Inbox = new InboxOptions { IsEnabled = false },
            DeadLetter = new DeadLetterOptions { IsEnabled = true }
        };

        var processor = new EventProcessor(
            scopeFactory,
            registry,
            CreateNoRetryPipeline(),
            Options.Create(options),
            NullLogger<EventProcessor>.Instance);

        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };
        var payload = SerializeEvent(@event);
        var eventName = EventNameResolver.GetEventName<TestOrderCreatedEvent>();

        // Act
        await processor.ProcessAsync(eventName, payload, null, null, @event.EventId);

        // Assert
        notifier.Verify(
            n => n.NotifyAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_Should_Not_Send_To_DeadLetter_When_Disabled()
    {
        // Arrange
        var registry = CreateRegistry<FailingHandler>();

        var deadLetterStore = new Mock<IDeadLetterStore>();
        var services = new ServiceCollection();
        services.AddSingleton<FailingHandler>();
        services.AddSingleton(deadLetterStore.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var options = new EksenEventBusOptions
        {
            AppName = "TestApp",
            Inbox = new InboxOptions { IsEnabled = false },
            DeadLetter = new DeadLetterOptions { IsEnabled = false }
        };

        var processor = new EventProcessor(
            scopeFactory,
            registry,
            CreateNoRetryPipeline(),
            Options.Create(options),
            NullLogger<EventProcessor>.Instance);

        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };
        var payload = SerializeEvent(@event);
        var eventName = EventNameResolver.GetEventName<TestOrderCreatedEvent>();

        // Act
        await processor.ProcessAsync(eventName, payload, null, null, @event.EventId);

        // Assert
        deadLetterStore.Verify(
            s => s.SaveAsync(It.IsAny<DeadLetterMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
