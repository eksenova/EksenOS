using Eksen.EventBus.DeadLetter;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.EventBus.Alerts.Tests;

public class DeadLetterAlertTests : EksenUnitTestBase
{
    [Fact]
    public void Summary_Should_Include_EventType_And_RetryCount_And_Error()
    {
        // Arrange
        var alert = new DeadLetterAlert
        {
            Message = new DeadLetterMessage
            {
                Id = Guid.NewGuid(),
                OriginalMessageId = Guid.NewGuid(),
                EventType = "OrderCreated",
                HandlerType = "OrderHandler",
                Payload = "{}",
                CreationTime = DateTime.UtcNow,
                FailedTime = DateTime.UtcNow,
                TotalRetryCount = 5,
                LastError = "Connection timeout"
            },
            AppName = "TestApp"
        };

        // Act
        var summary = alert.Summary;

        // Assert
        summary.ShouldContain("OrderCreated");
        summary.ShouldContain("5");
        summary.ShouldContain("OrderHandler");
        summary.ShouldContain("Connection timeout");
    }

    [Fact]
    public void Summary_Should_Show_NA_When_HandlerType_Is_Null()
    {
        // Arrange
        var alert = new DeadLetterAlert
        {
            Message = new DeadLetterMessage
            {
                Id = Guid.NewGuid(),
                OriginalMessageId = Guid.NewGuid(),
                EventType = "TestEvent",
                HandlerType = null,
                Payload = "{}",
                CreationTime = DateTime.UtcNow,
                FailedTime = DateTime.UtcNow,
                TotalRetryCount = 1,
                LastError = "Error"
            },
            AppName = "TestApp"
        };

        // Act
        var summary = alert.Summary;

        // Assert
        summary.ShouldContain("N/A");
    }

    [Fact]
    public void AlertTime_Should_Default_To_UtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var alert = new DeadLetterAlert
        {
            Message = new DeadLetterMessage
            {
                Id = Guid.NewGuid(),
                OriginalMessageId = Guid.NewGuid(),
                EventType = "TestEvent",
                Payload = "{}",
                CreationTime = DateTime.UtcNow,
                FailedTime = DateTime.UtcNow,
                TotalRetryCount = 1,
                LastError = "Error"
            },
            AppName = "TestApp"
        };

        // Assert
        alert.AlertTime.ShouldBeGreaterThanOrEqualTo(before);
        alert.AlertTime.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
    }
}
