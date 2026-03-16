using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.EventBus.RabbitMq;

public class RabbitMqEventRescheduler(
    IRabbitMqConnectionManager connectionManager,
    RabbitMqEventBusTransport transport,
    IOptions<RabbitMqEventBusOptions> options,
    ILogger<RabbitMqEventRescheduler> logger) : IEventRescheduler
{
    public async Task RescheduleAsync(
        string eventTypeName,
        string payload,
        string? correlationId,
        string? sourceApp,
        Guid eventId,
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Rescheduling event {EventType} ({EventId}) with delay {Delay}",
            eventTypeName,
            eventId,
            delay);

        if (delay <= TimeSpan.Zero)
        {
            await transport.PublishAsync(
                eventTypeName,
                payload,
                correlationId,
                sourceApp,
                targetApp: null,
                eventId,
                headers: null,
                cancellationToken);
            return;
        }

        var connection = await connectionManager.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        var rabbitOptions = options.Value;
        var delayQueueName = $"{rabbitOptions.ExchangeName}.delay.{(int)delay.TotalMilliseconds}ms";

        await channel.QueueDeclareAsync(
            queue: delayQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = rabbitOptions.ExchangeName,
                ["x-message-ttl"] = (int)delay.TotalMilliseconds
            },
            cancellationToken: cancellationToken);

        var body = System.Text.Encoding.UTF8.GetBytes(payload);
        var properties = new RabbitMQ.Client.BasicProperties
        {
            MessageId = eventId.ToString(),
            CorrelationId = correlationId,
            ContentType = "application/json",
            DeliveryMode = RabbitMQ.Client.DeliveryModes.Persistent,
            Headers = new Dictionary<string, object?>
            {
                ["x-event-type"] = eventTypeName,
                ["x-rescheduled"] = true
            }
        };

        if (sourceApp != null)
            properties.Headers["x-source-app"] = sourceApp;

        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: delayQueueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        logger.LogDebug(
            "Event {EventType} ({EventId}) rescheduled to delay queue {Queue}",
            eventTypeName,
            eventId,
            delayQueueName);
    }
}
