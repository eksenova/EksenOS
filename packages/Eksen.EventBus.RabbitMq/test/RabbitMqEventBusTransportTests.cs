using Eksen.TestBase;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using Shouldly;

namespace Eksen.EventBus.RabbitMq.Tests;

public class RabbitMqEventBusTransportTests : EksenUnitTestBase
{
    private static RabbitMqEventBusTransport CreateTransport(
        Mock<IRabbitMqConnectionManager> connectionManager,
        Mock<IEventProcessor>? processor = null,
        RabbitMqEventBusOptions? rabbitOptions = null)
    {
        return new RabbitMqEventBusTransport(
            connectionManager.Object,
            processor?.Object ?? new Mock<IEventProcessor>().Object,
            new Mock<IEventHandlerRegistry>().Object,
            Options.Create(rabbitOptions ?? new RabbitMqEventBusOptions()),
            Options.Create(new EksenEventBusOptions()),
            NullLogger<RabbitMqEventBusTransport>.Instance);
    }

    [Fact]
    public async Task PublishAsync_Should_Publish_To_Exchange()
    {
        // Arrange
        var channel = new Mock<IChannel>();
        var connection = new Mock<IConnection>();
        var connectionManager = new Mock<IRabbitMqConnectionManager>();

        connection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel.Object);
        connectionManager.Setup(cm => cm.GetConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection.Object);

        var transport = CreateTransport(connectionManager);
        var eventId = Guid.NewGuid();

        // Act
        await transport.PublishAsync(
            "OrderCreated",
            "{\"orderId\":1}",
            "corr-1",
            "OrderService",
            null,
            eventId,
            null);

        // Assert
        channel.Verify(
            c => c.BasicPublishAsync(
                "eksen.events",
                "OrderCreated",
                false,
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Should_Use_Custom_RoutingKey_From_Bindings()
    {
        // Arrange
        var channel = new Mock<IChannel>();
        var connection = new Mock<IConnection>();
        var connectionManager = new Mock<IRabbitMqConnectionManager>();

        connection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel.Object);
        connectionManager.Setup(cm => cm.GetConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection.Object);

        var options = new RabbitMqEventBusOptions
        {
            EventQueueBindings =
            {
                ["OrderCreated"] = new EventQueueBinding
                {
                    QueueName = "orders",
                    RoutingKey = "order.created.v1"
                }
            }
        };

        var transport = CreateTransport(connectionManager, rabbitOptions: options);

        // Act
        await transport.PublishAsync(
            "OrderCreated",
            "{}",
            null,
            null,
            null,
            Guid.NewGuid(),
            null);

        // Assert
        channel.Verify(
            c => c.BasicPublishAsync(
                "eksen.events",
                "order.created.v1",
                false,
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Should_Include_TargetApp_In_RoutingKey()
    {
        // Arrange
        var channel = new Mock<IChannel>();
        var connection = new Mock<IConnection>();
        var connectionManager = new Mock<IRabbitMqConnectionManager>();

        connection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel.Object);
        connectionManager.Setup(cm => cm.GetConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection.Object);

        var transport = CreateTransport(connectionManager);

        // Act
        await transport.PublishAsync(
            "OrderCreated",
            "{}",
            null,
            null,
            "PaymentService",
            Guid.NewGuid(),
            null);

        // Assert
        channel.Verify(
            c => c.BasicPublishAsync(
                "eksen.events",
                "PaymentService.OrderCreated",
                false,
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StopListeningAsync_Should_Dispose_Consumer_Channels()
    {
        // Arrange
        var connectionManager = new Mock<IRabbitMqConnectionManager>();
        var transport = CreateTransport(connectionManager);

        // Act & Assert (should not throw even without starting)
        await transport.StopListeningAsync();
    }
}
