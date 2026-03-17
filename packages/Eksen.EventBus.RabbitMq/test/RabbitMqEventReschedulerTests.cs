using Eksen.TestBase;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using Shouldly;

namespace Eksen.EventBus.RabbitMq.Tests;

public class RabbitMqEventReschedulerTests : EksenUnitTestBase
{
    [Fact]
    public async Task RescheduleAsync_Should_Publish_Directly_When_Delay_Is_Zero()
    {
        // Arrange
        var connectionManager = new Mock<IRabbitMqConnectionManager>();

        var processor = new Mock<IEventProcessor>();
        var handlerRegistry = new Mock<IEventHandlerRegistry>();
        var rabbitOptions = Options.Create(new RabbitMqEventBusOptions());
        var coreOptions = Options.Create(new EksenEventBusOptions());

        var transport = new RabbitMqEventBusTransport(
            connectionManager.Object,
            processor.Object,
            handlerRegistry.Object,
            rabbitOptions,
            coreOptions,
            NullLogger<RabbitMqEventBusTransport>.Instance);

        // Mock the channel for publish
        var channel = new Mock<IChannel>();
        var connection = new Mock<IConnection>();
        connection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel.Object);
        connectionManager.Setup(cm => cm.GetConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection.Object);

        var rescheduler = new RabbitMqEventRescheduler(
            connectionManager.Object,
            transport,
            rabbitOptions,
            NullLogger<RabbitMqEventRescheduler>.Instance);

        // Act
        await rescheduler.RescheduleAsync(
            "TestEvent",
            "{}",
            "corr-1",
            "src",
            Guid.NewGuid(),
            TimeSpan.Zero);

        // Assert - should publish via transport (which uses the channel)
        channel.Verify(
            c => c.BasicPublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RescheduleAsync_Should_Create_Delay_Queue_When_Delay_Is_Positive()
    {
        // Arrange
        var connectionManager = new Mock<IRabbitMqConnectionManager>();
        var channel = new Mock<IChannel>();
        var connection = new Mock<IConnection>();

        connection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel.Object);
        connectionManager.Setup(cm => cm.GetConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection.Object);

        var processor = new Mock<IEventProcessor>();
        var handlerRegistry = new Mock<IEventHandlerRegistry>();
        var rabbitOptions = Options.Create(new RabbitMqEventBusOptions());
        var coreOptions = Options.Create(new EksenEventBusOptions());

        var transport = new RabbitMqEventBusTransport(
            connectionManager.Object,
            processor.Object,
            handlerRegistry.Object,
            rabbitOptions,
            coreOptions,
            NullLogger<RabbitMqEventBusTransport>.Instance);

        var rescheduler = new RabbitMqEventRescheduler(
            connectionManager.Object,
            transport,
            rabbitOptions,
            NullLogger<RabbitMqEventRescheduler>.Instance);

        // Act
        await rescheduler.RescheduleAsync(
            "TestEvent",
            "{}",
            "corr-1",
            "src",
            Guid.NewGuid(),
            TimeSpan.FromSeconds(30));

        // Assert - should declare a delay queue
        channel.Verify(
            c => c.QueueDeclareAsync(
                It.Is<string>(q => q.Contains("delay") && q.Contains("30000ms")),
                true,
                false,
                false,
                It.IsAny<IDictionary<string, object?>?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Should publish to delay queue
        channel.Verify(
            c => c.BasicPublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
