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
    public async Task RescheduleAsync_Without_Delay_Publishes_To_The_Handler_Queue_Only()
    {
        var (rescheduler, channel) = Create();
        string? exchange = null;
        string? routingKey = null;
        channel.Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>(
                (e, r, _, _, _, _) =>
                {
                    exchange = e;
                    routingKey = r;
                })
            .Returns(ValueTask.CompletedTask);

        await rescheduler.RescheduleAsync(CreateRedelivery(), TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        // The default exchange routes by queue name, so only the handler's own queue receives the message.
        exchange.ShouldBe(string.Empty);
        routingKey.ShouldBe("orders.Orders.Handler");
    }

    [Fact]
    public async Task RescheduleAsync_With_Delay_Parks_The_Message_In_The_Covering_Tier()
    {
        var (rescheduler, channel) = Create();
        string? exchange = null;
        string? routingKey = null;
        BasicProperties? published = null;
        channel.Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>(
                (e, r, _, properties, _, _) =>
                {
                    exchange = e;
                    routingKey = r;
                    published = properties;
                })
            .Returns(ValueTask.CompletedTask);

        // Default tiers: 10s, 40s, 160s, ... (+20% jitter); 30s falls in tier 2.
        await rescheduler.RescheduleAsync(CreateRedelivery(), TimeSpan.FromSeconds(30), cancellationToken: TestContext.Current.CancellationToken);

        exchange.ShouldBe("eksen.events.delay");
        routingKey.ShouldBe("orders.Orders.Handler");
        published.ShouldNotBeNull();
        published.Expiration.ShouldBe("30000");
        published.Headers![RabbitMqHeaders.DelayTier].ShouldBe("2");
        channel.Verify(
            c => c.QueueDeclareAsync(
                "eksen.events.delay.2",
                true,
                false,
                false,
                It.Is<IDictionary<string, object?>?>(arguments => (string)arguments!["x-dead-letter-exchange"]! == string.Empty),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static EventRedelivery CreateRedelivery()
    {
        return new EventRedelivery
        {
            EventTypeName = "Orders.Created",
            Payload = "{}",
            EventId = Guid.NewGuid(),
            HandlerTypeName = "Orders.Handler",
            CorrelationId = "corr-1",
            SourceApp = "src"
        };
    }

    private static (RabbitMqEventRescheduler Rescheduler, Mock<IChannel> Channel) Create()
    {
        var channel = new Mock<IChannel>();
        channel.SetupGet(c => c.IsOpen).Returns(true);
        var connection = new Mock<IConnection>();
        connection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel.Object);
        var connectionManager = new Mock<IRabbitMqConnectionManager>();
        connectionManager.Setup(cm => cm.GetConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection.Object);

        var transport = new RabbitMqEventBusTransport(
            connectionManager.Object,
            new Mock<IEventProcessor>().Object,
            new Mock<IEventHandlerRegistry>().Object,
            Options.Create(new RabbitMqEventBusOptions()),
            Options.Create(new EksenEventBusOptions { AppName = "orders" }),
            NullLogger<RabbitMqEventBusTransport>.Instance);
        return (new RabbitMqEventRescheduler(transport), channel);
    }
}
