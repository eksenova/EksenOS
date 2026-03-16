namespace Eksen.EventBus.RabbitMq;

public class RabbitMqEventBusOptions
{
    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string VirtualHost { get; set; } = "/";

    public string ExchangeName { get; set; } = "eksen.events";

    public string ExchangeType { get; set; } = "topic";

    public bool ExchangeDurable { get; set; } = true;

    public bool ExchangeAutoDelete { get; set; }

    public string DeadLetterExchangeName { get; set; } = "eksen.events.dlx";

    public ushort PrefetchCount { get; set; } = 10;

    public bool AutomaticRecovery { get; set; } = true;

    public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(10);

    public Dictionary<string, EventQueueBinding> EventQueueBindings { get; set; } = [];

    public Dictionary<string, QueueOptions> QueueConfigurations { get; set; } = [];
}

public class EventQueueBinding
{
    public string QueueName { get; set; } = null!;

    public string RoutingKey { get; set; } = null!;
}

public class QueueOptions
{
    public bool Durable { get; set; } = true;

    public bool Exclusive { get; set; }

    public bool AutoDelete { get; set; }

    public int? MessageTtl { get; set; }

    public int? MaxLength { get; set; }

    public string? DeadLetterExchange { get; set; }

    public string? DeadLetterRoutingKey { get; set; }

    public Dictionary<string, object>? Arguments { get; set; }
}
