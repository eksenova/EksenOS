using Eksen.EventBus.DeadLetter;
using Eksen.EventBus.Inbox;
using Eksen.EventBus.Outbox;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.EventBus.Tests;

public class MessageModelTests : EksenUnitTestBase
{
    [Fact]
    public void OutboxMessage_Should_Initialize_Properties()
    {
        // Arrange & Act
        var msg = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "OrderCreated",
            Payload = "{}",
            CreationTime = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending,
            CorrelationId = "corr-1",
            SourceApp = "Src",
            TargetApp = "Tgt",
            Headers = new Dictionary<string, string> { ["k"] = "v" }
        };

        // Assert
        msg.Id.ShouldNotBe(Guid.Empty);
        msg.EventType.ShouldBe("OrderCreated");
        msg.Status.ShouldBe(OutboxMessageStatus.Pending);
        msg.RetryCount.ShouldBe(0);
        msg.ProcessedTime.ShouldBeNull();
        msg.LastError.ShouldBeNull();
        msg.Headers!["k"].ShouldBe("v");
    }

    [Fact]
    public void OutboxMessageStatus_Should_Have_Expected_Values()
    {
        // Assert
        ((int)OutboxMessageStatus.Pending).ShouldBe(0);
        ((int)OutboxMessageStatus.Processing).ShouldBe(1);
        ((int)OutboxMessageStatus.Processed).ShouldBe(2);
        ((int)OutboxMessageStatus.Failed).ShouldBe(3);
    }

    [Fact]
    public void InboxMessage_Should_Initialize_Properties()
    {
        // Arrange & Act
        var msg = new InboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = "OrderCreated",
            HandlerType = "OrderHandler",
            Payload = "{}",
            CreationTime = DateTime.UtcNow,
            Status = InboxMessageStatus.Processing,
            CorrelationId = "corr-1",
            SourceApp = "Src"
        };

        // Assert
        msg.Id.ShouldNotBe(Guid.Empty);
        msg.EventId.ShouldNotBe(Guid.Empty);
        msg.HandlerType.ShouldBe("OrderHandler");
        msg.Status.ShouldBe(InboxMessageStatus.Processing);
    }

    [Fact]
    public void InboxMessageStatus_Should_Have_Expected_Values()
    {
        // Assert
        ((int)InboxMessageStatus.Pending).ShouldBe(0);
        ((int)InboxMessageStatus.Processing).ShouldBe(1);
        ((int)InboxMessageStatus.Processed).ShouldBe(2);
        ((int)InboxMessageStatus.Failed).ShouldBe(3);
    }

    [Fact]
    public void DeadLetterMessage_Should_Initialize_Properties()
    {
        // Arrange & Act
        var msg = new DeadLetterMessage
        {
            Id = Guid.NewGuid(),
            OriginalMessageId = Guid.NewGuid(),
            EventType = "OrderCreated",
            HandlerType = "OrderHandler",
            Payload = "{}",
            CreationTime = DateTime.UtcNow,
            FailedTime = DateTime.UtcNow,
            TotalRetryCount = 5,
            LastError = "Boom",
            CorrelationId = "corr-1",
            SourceApp = "Src",
            TargetApp = "Tgt"
        };

        // Assert
        msg.Id.ShouldNotBe(Guid.Empty);
        msg.OriginalMessageId.ShouldNotBe(Guid.Empty);
        msg.TotalRetryCount.ShouldBe(5);
        msg.LastError.ShouldBe("Boom");
        msg.IsRequeued.ShouldBeFalse();
        msg.RequeuedTime.ShouldBeNull();
    }

    [Fact]
    public void OutboxMessageStats_Should_Initialize_Properties()
    {
        // Arrange & Act
        var stats = new OutboxMessageStats
        {
            Pending = 10,
            Processing = 5,
            Processed = 100,
            Failed = 2
        };

        // Assert
        stats.Pending.ShouldBe(10);
        stats.Processing.ShouldBe(5);
        stats.Processed.ShouldBe(100);
        stats.Failed.ShouldBe(2);
    }

    [Fact]
    public void InboxMessageStats_Should_Initialize_Properties()
    {
        // Arrange & Act
        var stats = new InboxMessageStats
        {
            Pending = 8,
            Processing = 3,
            Processed = 50,
            Failed = 1
        };

        // Assert
        stats.Pending.ShouldBe(8);
        stats.Processing.ShouldBe(3);
        stats.Processed.ShouldBe(50);
        stats.Failed.ShouldBe(1);
    }
}
