using Eksen.TestBase;
using Shouldly;

namespace Eksen.EventBus.Tests;

public class EventHandlerRegistryTests : EksenUnitTestBase
{
    private class TestOrderCreatedHandler : IEventHandler<TestOrderCreatedEvent>
    {
        public Task HandleAsync(TestOrderCreatedEvent @event, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private class AnotherOrderCreatedHandler : IEventHandler<TestOrderCreatedEvent>
    {
        public Task HandleAsync(TestOrderCreatedEvent @event, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private class TestPaymentHandler : IEventHandler<TestPaymentProcessedEvent>
    {
        public Task HandleAsync(TestPaymentProcessedEvent @event, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    [Fact]
    public void Register_Should_Add_Handler()
    {
        // Arrange
        var registry = new EventHandlerRegistry();

        // Act
        registry.Register<TestOrderCreatedEvent, TestOrderCreatedHandler>();

        // Assert
        var handlers = registry.GetHandlers<TestOrderCreatedEvent>();
        handlers.Count.ShouldBe(1);
        handlers[0].HandlerType.ShouldBe(typeof(TestOrderCreatedHandler));
        handlers[0].EventType.ShouldBe(typeof(TestOrderCreatedEvent));
    }

    [Fact]
    public void Register_Should_Support_Multiple_Handlers_For_Same_Event()
    {
        // Arrange
        var registry = new EventHandlerRegistry();

        // Act
        registry.Register<TestOrderCreatedEvent, TestOrderCreatedHandler>();
        registry.Register<TestOrderCreatedEvent, AnotherOrderCreatedHandler>();

        // Assert
        var handlers = registry.GetHandlers<TestOrderCreatedEvent>();
        handlers.Count.ShouldBe(2);
    }

    [Fact]
    public void Register_Should_Not_Add_Duplicate_Handlers()
    {
        // Arrange
        var registry = new EventHandlerRegistry();

        // Act
        registry.Register<TestOrderCreatedEvent, TestOrderCreatedHandler>();
        registry.Register<TestOrderCreatedEvent, TestOrderCreatedHandler>();

        // Assert
        var handlers = registry.GetHandlers<TestOrderCreatedEvent>();
        handlers.Count.ShouldBe(1);
    }

    [Fact]
    public void Register_Type_Should_Add_Handler()
    {
        // Arrange
        var registry = new EventHandlerRegistry();

        // Act
        registry.Register(typeof(TestOrderCreatedEvent), typeof(TestOrderCreatedHandler));

        // Assert
        var handlers = registry.GetHandlers<TestOrderCreatedEvent>();
        handlers.Count.ShouldBe(1);
    }

    [Fact]
    public void GetHandlers_ByString_Should_Return_Handlers()
    {
        // Arrange
        var registry = new EventHandlerRegistry();
        registry.Register<TestOrderCreatedEvent, TestOrderCreatedHandler>();
        var eventName = EventNameResolver.GetEventName<TestOrderCreatedEvent>();

        // Act
        var handlers = registry.GetHandlers(eventName);

        // Assert
        handlers.Count.ShouldBe(1);
    }

    [Fact]
    public void GetHandlers_Should_Return_Empty_When_No_Handlers_Registered()
    {
        // Arrange
        var registry = new EventHandlerRegistry();

        // Act
        var handlers = registry.GetHandlers("NonExistent.EventType");

        // Assert
        handlers.ShouldBeEmpty();
    }

    [Fact]
    public void GetAllEventTypes_Should_Return_All_Registered_Event_Types()
    {
        // Arrange
        var registry = new EventHandlerRegistry();
        registry.Register<TestOrderCreatedEvent, TestOrderCreatedHandler>();
        registry.Register<TestPaymentProcessedEvent, TestPaymentHandler>();

        // Act
        var eventTypes = registry.GetAllEventTypes();

        // Assert
        eventTypes.Count.ShouldBe(2);
        eventTypes.ShouldContain(EventNameResolver.GetEventName<TestOrderCreatedEvent>());
        eventTypes.ShouldContain(EventNameResolver.GetEventName<TestPaymentProcessedEvent>());
    }

    [Fact]
    public void GetAllHandlers_Should_Return_All_Registered_Handlers()
    {
        // Arrange
        var registry = new EventHandlerRegistry();
        registry.Register<TestOrderCreatedEvent, TestOrderCreatedHandler>();
        registry.Register<TestOrderCreatedEvent, AnotherOrderCreatedHandler>();
        registry.Register<TestPaymentProcessedEvent, TestPaymentHandler>();

        // Act
        var handlers = registry.GetAllHandlers();

        // Assert
        handlers.Count.ShouldBe(3);
    }

    [Fact]
    public void EventHandlerDescriptor_Should_Expose_EventTypeName()
    {
        // Arrange
        var registry = new EventHandlerRegistry();
        registry.Register<TestOrderCreatedEvent, TestOrderCreatedHandler>();

        // Act
        var descriptor = registry.GetHandlers<TestOrderCreatedEvent>()[0];

        // Assert
        descriptor.EventTypeName.ShouldBe(EventNameResolver.GetEventName<TestOrderCreatedEvent>());
    }

    [Fact]
    public void EventHandlerDescriptor_Should_Expose_HandlerTypeName()
    {
        // Arrange
        var registry = new EventHandlerRegistry();
        registry.Register<TestOrderCreatedEvent, TestOrderCreatedHandler>();

        // Act
        var descriptor = registry.GetHandlers<TestOrderCreatedEvent>()[0];

        // Assert
        descriptor.HandlerTypeName.ShouldBe(typeof(TestOrderCreatedHandler).FullName);
    }
}
