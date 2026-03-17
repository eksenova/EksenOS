using Eksen.TestBase;
using Shouldly;

namespace Eksen.EventBus.Tests;

public class IntegrationEventTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_Should_Set_EventId()
    {
        // Arrange & Act
        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };

        // Assert
        @event.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Constructor_Should_Set_CreationTime()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };

        // Assert
        @event.CreationTime.ShouldBeGreaterThanOrEqualTo(before);
        @event.CreationTime.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
    }

    [Fact]
    public void Constructor_Should_Set_CorrelationId_To_Null_By_Default()
    {
        // Arrange & Act
        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };

        // Assert
        @event.CorrelationId.ShouldBeNull();
    }

    [Fact]
    public void Constructor_Should_Set_SourceApp_To_Null_By_Default()
    {
        // Arrange & Act
        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };

        // Assert
        @event.SourceApp.ShouldBeNull();
    }

    [Fact]
    public void Init_Properties_Should_Allow_Setting_CorrelationId()
    {
        // Arrange & Act
        var @event = new TestOrderCreatedEvent
        {
            OrderNumber = "ORD-1",
            Amount = 100m,
            CorrelationId = "corr-123"
        };

        // Assert
        @event.CorrelationId.ShouldBe("corr-123");
    }

    [Fact]
    public void Init_Properties_Should_Allow_Setting_SourceApp()
    {
        // Arrange & Act
        var @event = new TestOrderCreatedEvent
        {
            OrderNumber = "ORD-1",
            Amount = 100m,
            SourceApp = "OrderService"
        };

        // Assert
        @event.SourceApp.ShouldBe("OrderService");
    }

    [Fact]
    public void Two_Events_Should_Have_Different_EventIds()
    {
        // Arrange & Act
        var event1 = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };
        var event2 = new TestOrderCreatedEvent { OrderNumber = "ORD-2", Amount = 200m };

        // Assert
        event1.EventId.ShouldNotBe(event2.EventId);
    }
}
