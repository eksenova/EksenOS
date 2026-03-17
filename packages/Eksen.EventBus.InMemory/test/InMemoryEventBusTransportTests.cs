using Eksen.TestBase;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Eksen.EventBus.InMemory.Tests;

public class InMemoryEventBusTransportTests : EksenUnitTestBase
{
    [Fact]
    public async Task PublishAsync_Should_Deliver_To_Listener()
    {
        // Arrange
        string? receivedPayload = null;
        string? receivedEventType = null;

        var processor = new Mock<IEventProcessor>();
        processor
            .Setup(p => p.ProcessAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, string?, Guid, CancellationToken>(
                (eventType, payload, _, _, _, _) =>
                {
                    receivedEventType = eventType;
                    receivedPayload = payload;
                })
            .Returns(Task.CompletedTask);

        var transport = new InMemoryEventBusTransport(
            processor.Object,
            NullLogger<InMemoryEventBusTransport>.Instance);

        // Start listening in background
        var listenTask = Task.Run(() => transport.StartListeningAsync());

        // Give time for listener to start
        await Task.Delay(50);

        // Act
        await transport.PublishAsync(
            "TestEvent",
            "{\"data\":1}",
            "corr-1",
            "src",
            "tgt",
            Guid.NewGuid(),
            null);

        // Wait for processing
        await Task.Delay(100);

        // Stop
        await transport.StopListeningAsync();

        // Assert
        receivedEventType.ShouldBe("TestEvent");
        receivedPayload.ShouldBe("{\"data\":1}");
    }

    [Fact]
    public async Task StopListeningAsync_Should_Complete_Channel()
    {
        // Arrange
        var processor = new Mock<IEventProcessor>();
        var transport = new InMemoryEventBusTransport(
            processor.Object,
            NullLogger<InMemoryEventBusTransport>.Instance);

        var listenTask = Task.Run(() => transport.StartListeningAsync());
        await Task.Delay(50);

        // Act
        await transport.StopListeningAsync();

        // Assert - listener should complete within a reasonable time
        var completed = await Task.WhenAny(listenTask, Task.Delay(2000));
        completed.ShouldBe(listenTask);
    }

    [Fact]
    public async Task PublishAsync_Should_Pass_CorrectId_To_Processor()
    {
        // Arrange
        Guid? receivedId = null;

        var processor = new Mock<IEventProcessor>();
        processor
            .Setup(p => p.ProcessAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, string?, Guid, CancellationToken>(
                (_, _, _, _, id, _) => receivedId = id)
            .Returns(Task.CompletedTask);

        var transport = new InMemoryEventBusTransport(
            processor.Object,
            NullLogger<InMemoryEventBusTransport>.Instance);

        var listenTask = Task.Run(() => transport.StartListeningAsync());
        await Task.Delay(50);

        var eventId = Guid.NewGuid();

        // Act
        await transport.PublishAsync("TestEvent", "{}", null, null, null, eventId, null);
        await Task.Delay(100);
        await transport.StopListeningAsync();

        // Assert
        receivedId.ShouldBe(eventId);
    }

    [Fact]
    public async Task PublishAsync_Should_Handle_Multiple_Messages()
    {
        // Arrange
        var processedCount = 0;

        var processor = new Mock<IEventProcessor>();
        processor
            .Setup(p => p.ProcessAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref processedCount))
            .Returns(Task.CompletedTask);

        var transport = new InMemoryEventBusTransport(
            processor.Object,
            NullLogger<InMemoryEventBusTransport>.Instance);

        var listenTask = Task.Run(() => transport.StartListeningAsync());
        await Task.Delay(50);

        // Act
        for (var i = 0; i < 5; i++)
            await transport.PublishAsync("TestEvent", "{}", null, null, null, Guid.NewGuid(), null);

        await Task.Delay(200);
        await transport.StopListeningAsync();

        // Assert
        processedCount.ShouldBe(5);
    }
}
