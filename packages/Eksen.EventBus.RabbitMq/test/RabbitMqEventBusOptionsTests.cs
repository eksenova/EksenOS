using Eksen.TestBase;
using Shouldly;

namespace Eksen.EventBus.RabbitMq.Tests;

public class RabbitMqEventBusOptionsTests : EksenUnitTestBase
{
    [Fact]
    public void Defaults_Should_Have_Expected_Values()
    {
        // Arrange & Act
        var options = new RabbitMqEventBusOptions();

        // Assert
        options.HostName.ShouldBe("localhost");
        options.Port.ShouldBe(5672);
        options.UserName.ShouldBe("guest");
        options.Password.ShouldBe("guest");
        options.VirtualHost.ShouldBe("/");
        options.ExchangeName.ShouldBe("eksen.events");
        options.ExchangeType.ShouldBe("topic");
        options.ExchangeDurable.ShouldBeTrue();
        options.ExchangeAutoDelete.ShouldBeFalse();
        options.DeadLetterExchangeName.ShouldBe("eksen.events.dlx");
        options.PrefetchCount.ShouldBe((ushort)10);
        options.AutomaticRecovery.ShouldBeTrue();
        options.NetworkRecoveryInterval.ShouldBe(TimeSpan.FromSeconds(10));
        options.EventQueueBindings.ShouldBeEmpty();
        options.QueueConfigurations.ShouldBeEmpty();
    }

    [Fact]
    public void EventQueueBinding_Should_Set_Properties()
    {
        // Arrange & Act
        var binding = new EventQueueBinding
        {
            QueueName = "my-queue",
            RoutingKey = "order.created"
        };

        // Assert
        binding.QueueName.ShouldBe("my-queue");
        binding.RoutingKey.ShouldBe("order.created");
    }

    [Fact]
    public void QueueOptions_Should_Have_Defaults()
    {
        // Arrange & Act
        var opts = new QueueOptions();

        // Assert
        opts.Durable.ShouldBeTrue();
        opts.Exclusive.ShouldBeFalse();
        opts.AutoDelete.ShouldBeFalse();
        opts.MessageTtl.ShouldBeNull();
        opts.MaxLength.ShouldBeNull();
        opts.DeadLetterExchange.ShouldBeNull();
        opts.DeadLetterRoutingKey.ShouldBeNull();
        opts.Arguments.ShouldBeNull();
    }

    [Fact]
    public void QueueOptions_Should_Set_Properties()
    {
        // Arrange & Act
        var opts = new QueueOptions
        {
            Durable = false,
            Exclusive = true,
            AutoDelete = true,
            MessageTtl = 60000,
            MaxLength = 1000,
            DeadLetterExchange = "my-dlx",
            DeadLetterRoutingKey = "dlx-key",
            Arguments = new Dictionary<string, object> { ["x-custom"] = "val" }
        };

        // Assert
        opts.Durable.ShouldBeFalse();
        opts.Exclusive.ShouldBeTrue();
        opts.AutoDelete.ShouldBeTrue();
        opts.MessageTtl.ShouldBe(60000);
        opts.MaxLength.ShouldBe(1000);
        opts.DeadLetterExchange.ShouldBe("my-dlx");
        opts.DeadLetterRoutingKey.ShouldBe("dlx-key");
        opts.Arguments!["x-custom"].ShouldBe("val");
    }

    [Fact]
    public void EventQueueBindings_Should_Support_Multiple_Bindings()
    {
        // Arrange
        var options = new RabbitMqEventBusOptions
        {
            EventQueueBindings =
            {
                ["OrderCreated"] = new EventQueueBinding { QueueName = "orders", RoutingKey = "order.created" },
                ["PaymentProcessed"] = new EventQueueBinding { QueueName = "payments", RoutingKey = "payment.done" }
            }
        };

        // Assert
        options.EventQueueBindings.Count.ShouldBe(2);
        options.EventQueueBindings["OrderCreated"].RoutingKey.ShouldBe("order.created");
        options.EventQueueBindings["PaymentProcessed"].QueueName.ShouldBe("payments");
    }
}
