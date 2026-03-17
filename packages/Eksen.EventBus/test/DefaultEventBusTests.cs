using Eksen.EventBus.Outbox;
using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Eksen.EventBus.Tests;

public class DefaultEventBusTests : EksenUnitTestBase
{
    private readonly Mock<IEventBusTransport> _transport = new();
    private readonly JsonEventSerializer _serializer = new();

    private DefaultEventBus CreateEventBus(
        EksenEventBusOptions? options = null,
        IOutboxStore? outboxStore = null)
    {
        var opts = Options.Create(options ?? new EksenEventBusOptions { AppName = "TestApp" });

        var services = new ServiceCollection();
        if (outboxStore != null)
            services.AddSingleton(outboxStore);

        var scopeFactory = services.BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        return new DefaultEventBus(_transport.Object, _serializer, opts, scopeFactory);
    }

    [Fact]
    public async Task PublishAsync_Should_Use_Transport_When_Outbox_Disabled()
    {
        // Arrange
        var options = new EksenEventBusOptions
        {
            AppName = "TestApp",
            Outbox = new OutboxOptions { IsEnabled = false }
        };
        var bus = CreateEventBus(options);
        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };

        // Act
        await bus.PublishAsync(@event);

        // Assert
        _transport.Verify(
            t => t.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                "TestApp",
                It.IsAny<string?>(),
                @event.EventId,
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Should_Use_Outbox_When_Outbox_Enabled()
    {
        // Arrange
        var outboxStore = new Mock<IOutboxStore>();
        var options = new EksenEventBusOptions
        {
            AppName = "TestApp",
            Outbox = new OutboxOptions { IsEnabled = true }
        };

        var services = new ServiceCollection();
        services.AddSingleton(outboxStore.Object);
        var scopeFactory = services.BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        var bus = new DefaultEventBus(
            _transport.Object,
            _serializer,
            Options.Create(options),
            scopeFactory);

        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };

        // Act
        await bus.PublishAsync(@event);

        // Assert
        outboxStore.Verify(
            s => s.SaveAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _transport.Verify(
            t => t.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishAsync_Should_Use_Event_CorrelationId_When_Options_Null()
    {
        // Arrange
        var options = new EksenEventBusOptions
        {
            AppName = "TestApp",
            Outbox = new OutboxOptions { IsEnabled = false }
        };
        var bus = CreateEventBus(options);
        var @event = new TestOrderCreatedEvent
        {
            OrderNumber = "ORD-1",
            Amount = 100m,
            CorrelationId = "event-corr-id"
        };

        // Act
        await bus.PublishAsync(@event);

        // Assert
        _transport.Verify(
            t => t.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                "event-corr-id",
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Should_Use_Options_CorrelationId_Over_Event()
    {
        // Arrange
        var options = new EksenEventBusOptions
        {
            AppName = "TestApp",
            Outbox = new OutboxOptions { IsEnabled = false }
        };
        var bus = CreateEventBus(options);
        var @event = new TestOrderCreatedEvent
        {
            OrderNumber = "ORD-1",
            Amount = 100m,
            CorrelationId = "event-corr-id"
        };
        var publishOptions = new PublishOptions { CorrelationId = "options-corr-id" };

        // Act
        await bus.PublishAsync(@event, publishOptions);

        // Assert
        _transport.Verify(
            t => t.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                "options-corr-id",
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Should_Use_AppName_When_Event_SourceApp_Is_Null()
    {
        // Arrange
        var options = new EksenEventBusOptions
        {
            AppName = "MyService",
            Outbox = new OutboxOptions { IsEnabled = false }
        };
        var bus = CreateEventBus(options);
        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };

        // Act
        await bus.PublishAsync(@event);

        // Assert
        _transport.Verify(
            t => t.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                "MyService",
                It.IsAny<string?>(),
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Should_Use_Event_SourceApp_Over_AppName()
    {
        // Arrange
        var options = new EksenEventBusOptions
        {
            AppName = "MyService",
            Outbox = new OutboxOptions { IsEnabled = false }
        };
        var bus = CreateEventBus(options);
        var @event = new TestOrderCreatedEvent
        {
            OrderNumber = "ORD-1",
            Amount = 100m,
            SourceApp = "CustomApp"
        };

        // Act
        await bus.PublishAsync(@event);

        // Assert
        _transport.Verify(
            t => t.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                "CustomApp",
                It.IsAny<string?>(),
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Should_Pass_TargetApp_From_Options()
    {
        // Arrange
        var options = new EksenEventBusOptions
        {
            AppName = "TestApp",
            Outbox = new OutboxOptions { IsEnabled = false }
        };
        var bus = CreateEventBus(options);
        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };
        var publishOptions = new PublishOptions { TargetApp = "PaymentService" };

        // Act
        await bus.PublishAsync(@event, publishOptions);

        // Assert
        _transport.Verify(
            t => t.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                "PaymentService",
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Should_Pass_Headers_From_Options()
    {
        // Arrange
        var options = new EksenEventBusOptions
        {
            AppName = "TestApp",
            Outbox = new OutboxOptions { IsEnabled = false }
        };
        var bus = CreateEventBus(options);
        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };
        var headers = new Dictionary<string, string> { ["x-custom"] = "value" };
        var publishOptions = new PublishOptions { Headers = headers };

        // Act
        await bus.PublishAsync(@event, publishOptions);

        // Assert
        _transport.Verify(
            t => t.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid>(),
                headers,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Without_Options_Should_Delegate_To_Overload_With_Null_Options()
    {
        // Arrange
        var options = new EksenEventBusOptions
        {
            AppName = "TestApp",
            Outbox = new OutboxOptions { IsEnabled = false }
        };
        var bus = CreateEventBus(options);
        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };

        // Act
        await bus.PublishAsync(@event);

        // Assert
        _transport.Verify(
            t => t.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Should_Store_Correct_OutboxMessage_Fields()
    {
        // Arrange
        OutboxMessage? savedMessage = null;
        var outboxStore = new Mock<IOutboxStore>();
        outboxStore
            .Setup(s => s.SaveAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, CancellationToken>((m, _) => savedMessage = m)
            .Returns(Task.CompletedTask);

        var options = new EksenEventBusOptions
        {
            AppName = "TestApp",
            Outbox = new OutboxOptions { IsEnabled = true }
        };

        var services = new ServiceCollection();
        services.AddSingleton(outboxStore.Object);
        var scopeFactory = services.BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        var bus = new DefaultEventBus(
            _transport.Object,
            _serializer,
            Options.Create(options),
            scopeFactory);

        var @event = new TestOrderCreatedEvent
        {
            OrderNumber = "ORD-1",
            Amount = 100m,
            CorrelationId = "corr-42",
            SourceApp = "CustomSrc"
        };
        var publishOptions = new PublishOptions
        {
            TargetApp = "TargetSvc",
            Headers = new Dictionary<string, string> { ["h1"] = "v1" }
        };

        // Act
        await bus.PublishAsync(@event, publishOptions);

        // Assert
        savedMessage.ShouldNotBeNull();
        savedMessage.Id.ShouldNotBe(Guid.Empty);
        savedMessage.EventType.ShouldBe(EventNameResolver.GetEventName<TestOrderCreatedEvent>());
        savedMessage.Payload.ShouldNotBeNullOrWhiteSpace();
        savedMessage.Status.ShouldBe(OutboxMessageStatus.Pending);
        savedMessage.CorrelationId.ShouldBe("corr-42");
        savedMessage.SourceApp.ShouldBe("CustomSrc");
        savedMessage.TargetApp.ShouldBe("TargetSvc");
        savedMessage.Headers.ShouldNotBeNull();
        savedMessage.Headers["h1"].ShouldBe("v1");
    }
}
