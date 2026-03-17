using Eksen.EventBus.DeadLetter;
using Eksen.TestBase;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Eksen.EventBus.Alerts.Tests;

public class DeadLetterAlertManagerTests : EksenUnitTestBase
{
    private static DeadLetterMessage CreateDeadLetterMessage() => new()
    {
        Id = Guid.NewGuid(),
        OriginalMessageId = Guid.NewGuid(),
        EventType = "OrderCreated",
        HandlerType = "OrderHandler",
        Payload = "{}",
        CreationTime = DateTime.UtcNow,
        FailedTime = DateTime.UtcNow,
        TotalRetryCount = 3,
        LastError = "Boom"
    };

    [Fact]
    public async Task SendAlertAsync_Should_Not_Send_When_Disabled()
    {
        // Arrange
        var channel = new Mock<IDeadLetterAlertChannel>();
        var alertOptions = Options.Create(new EksenEventBusAlertOptions { IsEnabled = false });
        var eventBusOptions = Options.Create(new EksenEventBusOptions());

        var manager = new DeadLetterAlertManager(
            [channel.Object],
            alertOptions,
            eventBusOptions,
            NullLogger<DeadLetterAlertManager>.Instance);

        // Act
        await manager.SendAlertAsync(CreateDeadLetterMessage());

        // Assert
        channel.Verify(
            c => c.SendAlertAsync(It.IsAny<DeadLetterAlert>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAlertAsync_Should_Send_To_All_Channels_When_None_Specified()
    {
        // Arrange
        var channel1 = new Mock<IDeadLetterAlertChannel>();
        channel1.Setup(c => c.Name).Returns("Channel1");
        var channel2 = new Mock<IDeadLetterAlertChannel>();
        channel2.Setup(c => c.Name).Returns("Channel2");

        var alertOptions = Options.Create(new EksenEventBusAlertOptions
        {
            IsEnabled = true,
            EnabledChannels = []
        });
        var eventBusOptions = Options.Create(new EksenEventBusOptions { AppName = "TestApp" });

        var manager = new DeadLetterAlertManager(
            [channel1.Object, channel2.Object],
            alertOptions,
            eventBusOptions,
            NullLogger<DeadLetterAlertManager>.Instance);

        // Act
        await manager.SendAlertAsync(CreateDeadLetterMessage());

        // Assert
        channel1.Verify(
            c => c.SendAlertAsync(It.IsAny<DeadLetterAlert>(), It.IsAny<CancellationToken>()),
            Times.Once);
        channel2.Verify(
            c => c.SendAlertAsync(It.IsAny<DeadLetterAlert>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAlertAsync_Should_Filter_By_EnabledChannels()
    {
        // Arrange
        var channel1 = new Mock<IDeadLetterAlertChannel>();
        channel1.Setup(c => c.Name).Returns("Slack");
        var channel2 = new Mock<IDeadLetterAlertChannel>();
        channel2.Setup(c => c.Name).Returns("Email");

        var alertOptions = Options.Create(new EksenEventBusAlertOptions
        {
            IsEnabled = true,
            EnabledChannels = ["Slack"]
        });
        var eventBusOptions = Options.Create(new EksenEventBusOptions());

        var manager = new DeadLetterAlertManager(
            [channel1.Object, channel2.Object],
            alertOptions,
            eventBusOptions,
            NullLogger<DeadLetterAlertManager>.Instance);

        // Act
        await manager.SendAlertAsync(CreateDeadLetterMessage());

        // Assert
        channel1.Verify(
            c => c.SendAlertAsync(It.IsAny<DeadLetterAlert>(), It.IsAny<CancellationToken>()),
            Times.Once);
        channel2.Verify(
            c => c.SendAlertAsync(It.IsAny<DeadLetterAlert>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAlertAsync_Should_Pass_Correct_Alert_Details()
    {
        // Arrange
        DeadLetterAlert? capturedAlert = null;
        var channel = new Mock<IDeadLetterAlertChannel>();
        channel.Setup(c => c.Name).Returns("Test");
        channel
            .Setup(c => c.SendAlertAsync(It.IsAny<DeadLetterAlert>(), It.IsAny<CancellationToken>()))
            .Callback<DeadLetterAlert, CancellationToken>((a, _) => capturedAlert = a)
            .Returns(Task.CompletedTask);

        var alertOptions = Options.Create(new EksenEventBusAlertOptions { IsEnabled = true });
        var eventBusOptions = Options.Create(new EksenEventBusOptions { AppName = "MyService" });

        var manager = new DeadLetterAlertManager(
            [channel.Object],
            alertOptions,
            eventBusOptions,
            NullLogger<DeadLetterAlertManager>.Instance);

        var message = CreateDeadLetterMessage();

        // Act
        await manager.SendAlertAsync(message);

        // Assert
        capturedAlert.ShouldNotBeNull();
        capturedAlert.Message.ShouldBe(message);
        capturedAlert.AppName.ShouldBe("MyService");
    }

    [Fact]
    public async Task SendAlertAsync_Should_Continue_When_Channel_Throws()
    {
        // Arrange
        var failingChannel = new Mock<IDeadLetterAlertChannel>();
        failingChannel.Setup(c => c.Name).Returns("Failing");
        failingChannel
            .Setup(c => c.SendAlertAsync(It.IsAny<DeadLetterAlert>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Channel error"));

        var successChannel = new Mock<IDeadLetterAlertChannel>();
        successChannel.Setup(c => c.Name).Returns("Success");

        var alertOptions = Options.Create(new EksenEventBusAlertOptions { IsEnabled = true });
        var eventBusOptions = Options.Create(new EksenEventBusOptions());

        var manager = new DeadLetterAlertManager(
            [failingChannel.Object, successChannel.Object],
            alertOptions,
            eventBusOptions,
            NullLogger<DeadLetterAlertManager>.Instance);

        // Act (should not throw)
        await manager.SendAlertAsync(CreateDeadLetterMessage());

        // Assert - success channel was still called
        successChannel.Verify(
            c => c.SendAlertAsync(It.IsAny<DeadLetterAlert>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
