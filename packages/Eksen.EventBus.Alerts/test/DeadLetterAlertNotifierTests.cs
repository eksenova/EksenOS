using Eksen.EventBus.DeadLetter;
using Eksen.TestBase;
using Moq;
using Shouldly;

namespace Eksen.EventBus.Alerts.Tests;

public class DeadLetterAlertNotifierTests : EksenUnitTestBase
{
    [Fact]
    public async Task NotifyAsync_Should_Delegate_To_AlertManager()
    {
        // Arrange
        var alertManager = new Mock<IDeadLetterAlertManager>();
        var notifier = new DeadLetterAlertNotifier(alertManager.Object);
        var message = new DeadLetterMessage
        {
            Id = Guid.NewGuid(),
            OriginalMessageId = Guid.NewGuid(),
            EventType = "TestEvent",
            HandlerType = "TestHandler",
            Payload = "{}",
            CreationTime = DateTime.UtcNow,
            FailedTime = DateTime.UtcNow,
            TotalRetryCount = 3,
            LastError = "error"
        };

        // Act
        await notifier.NotifyAsync(message);

        // Assert
        alertManager.Verify(
            m => m.SendAlertAsync(message, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
