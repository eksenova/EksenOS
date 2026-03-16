using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Eksen.EventBus.RabbitMq;

public class RabbitMqEventBusTransport(
    IRabbitMqConnectionManager connectionManager,
    IEventProcessor processor,
    IEventHandlerRegistry handlerRegistry,
    IOptions<RabbitMqEventBusOptions> options,
    IOptions<EksenEventBusOptions> eventBusOptions,
    ILogger<RabbitMqEventBusTransport> logger) : IEventBusTransport
{
    private IChannel? _publishChannel;
    private readonly List<IChannel> _consumerChannels = [];
    private readonly SemaphoreSlim _publishSemaphore = new(1, 1);

    public async Task PublishAsync(
        string eventTypeName,
        string payload,
        string? correlationId,
        string? sourceApp,
        string? targetApp,
        Guid eventId,
        Dictionary<string, string>? headers,
        CancellationToken cancellationToken = default)
    {
        var channel = await GetPublishChannelAsync(cancellationToken);
        var rabbitOptions = options.Value;

        var routingKey = GetRoutingKey(eventTypeName, targetApp);
        var body = Encoding.UTF8.GetBytes(payload);

        var properties = new BasicProperties
        {
            MessageId = eventId.ToString(),
            CorrelationId = correlationId,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>()
        };

        properties.Headers["x-event-type"] = eventTypeName;

        if (sourceApp != null)
            properties.Headers["x-source-app"] = sourceApp;

        if (targetApp != null)
            properties.Headers["x-target-app"] = targetApp;

        if (headers != null)
        {
            foreach (var (key, value) in headers)
                properties.Headers[key] = value;
        }

        await channel.BasicPublishAsync(
            exchange: rabbitOptions.ExchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        logger.LogDebug(
            "Published event {EventType} with routing key {RoutingKey}",
            eventTypeName,
            routingKey);
    }

    public async Task StartListeningAsync(CancellationToken cancellationToken = default)
    {
        var connection = await connectionManager.GetConnectionAsync(cancellationToken);
        var rabbitOptions = options.Value;
        var appName = eventBusOptions.Value.AppName;

        var setupChannel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await SetupExchangesAsync(setupChannel, rabbitOptions, cancellationToken);

        var handlerDescriptors = handlerRegistry.GetAllHandlers();
        var handlersByEvent = handlerDescriptors
            .GroupBy(h => h.EventTypeName)
            .ToList();

        foreach (var eventGroup in handlersByEvent)
        {
            var eventTypeName = eventGroup.Key;

            foreach (var descriptor in eventGroup)
            {
                var queueName = GetQueueName(appName, descriptor.HandlerType);
                var routingKey = GetRoutingKey(eventTypeName, targetApp: null);

                var queueOptions = GetQueueOptions(rabbitOptions, queueName);
                var arguments = BuildQueueArguments(rabbitOptions, queueOptions);

                var consumerChannel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
                await consumerChannel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: rabbitOptions.PrefetchCount,
                    global: false,
                    cancellationToken: cancellationToken);

                await consumerChannel.QueueDeclareAsync(
                    queue: queueName,
                    durable: queueOptions.Durable,
                    exclusive: queueOptions.Exclusive,
                    autoDelete: queueOptions.AutoDelete,
                    arguments: arguments,
                    cancellationToken: cancellationToken);

                await consumerChannel.QueueBindAsync(
                    queue: queueName,
                    exchange: rabbitOptions.ExchangeName,
                    routingKey: routingKey,
                    cancellationToken: cancellationToken);

                var consumer = new AsyncEventingBasicConsumer(consumerChannel);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    try
                    {
                        var body = Encoding.UTF8.GetString(ea.Body.Span);
                        var evtType = eventTypeName;

                        if (ea.BasicProperties.Headers?.TryGetValue("x-event-type", out var typeHeader) == true
                            && typeHeader is byte[] typeBytes)
                        {
                            evtType = Encoding.UTF8.GetString(typeBytes);
                        }

                        var corrId = ea.BasicProperties.CorrelationId;
                        string? srcApp = null;
                        if (ea.BasicProperties.Headers?.TryGetValue("x-source-app", out var srcHeader) == true
                            && srcHeader is byte[] srcBytes)
                        {
                            srcApp = Encoding.UTF8.GetString(srcBytes);
                        }

                        var eventId = Guid.TryParse(ea.BasicProperties.MessageId, out var id) ? id : Guid.NewGuid();

                        await processor.ProcessAsync(evtType, body, corrId, srcApp, eventId, cancellationToken);

                        await consumerChannel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing message from queue {Queue}", queueName);
                        await consumerChannel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken);
                    }
                };

                await consumerChannel.BasicConsumeAsync(
                    queue: queueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: cancellationToken);

                _consumerChannels.Add(consumerChannel);

                logger.LogInformation(
                    "Listening on queue {Queue} for event {EventType} with handler {HandlerType}",
                    queueName,
                    eventTypeName,
                    descriptor.HandlerTypeName);
            }
        }

        await setupChannel.DisposeAsync();
    }

    public async Task StopListeningAsync(CancellationToken cancellationToken = default)
    {
        foreach (var channel in _consumerChannels)
        {
            await channel.DisposeAsync();
        }

        _consumerChannels.Clear();
        logger.LogInformation("RabbitMQ event bus listener stopped");
    }

    private async Task SetupExchangesAsync(
        IChannel channel,
        RabbitMqEventBusOptions rabbitOptions,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            exchange: rabbitOptions.ExchangeName,
            type: rabbitOptions.ExchangeType,
            durable: rabbitOptions.ExchangeDurable,
            autoDelete: rabbitOptions.ExchangeAutoDelete,
            cancellationToken: cancellationToken);

        if (rabbitOptions.DeadLetterExchangeName != null)
        {
            await channel.ExchangeDeclareAsync(
                exchange: rabbitOptions.DeadLetterExchangeName,
                type: "topic",
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);
        }
    }

    private static string GetQueueName(string appName, Type handlerType)
    {
        var handlerName = handlerType.FullName ?? handlerType.Name;
        return $"{appName}.{handlerName}";
    }

    private string GetRoutingKey(string eventTypeName, string? targetApp)
    {
        var rabbitOptions = options.Value;

        if (rabbitOptions.EventQueueBindings.TryGetValue(eventTypeName, out var binding))
            return binding.RoutingKey;

        return targetApp != null
            ? $"{targetApp}.{eventTypeName}"
            : eventTypeName;
    }

    private static QueueOptions GetQueueOptions(RabbitMqEventBusOptions rabbitOptions, string queueName)
    {
        return rabbitOptions.QueueConfigurations.TryGetValue(queueName, out var queueOptions)
            ? queueOptions
            : new QueueOptions();
    }

    private static Dictionary<string, object?> BuildQueueArguments(
        RabbitMqEventBusOptions rabbitOptions,
        QueueOptions queueOptions)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = queueOptions.DeadLetterExchange ?? rabbitOptions.DeadLetterExchangeName
        };

        if (queueOptions.DeadLetterRoutingKey != null)
            arguments["x-dead-letter-routing-key"] = queueOptions.DeadLetterRoutingKey;

        if (queueOptions.MessageTtl.HasValue)
            arguments["x-message-ttl"] = queueOptions.MessageTtl.Value;

        if (queueOptions.MaxLength.HasValue)
            arguments["x-max-length"] = queueOptions.MaxLength.Value;

        if (queueOptions.Arguments != null)
        {
            foreach (var (key, value) in queueOptions.Arguments)
                arguments[key] = value;
        }

        return arguments;
    }

    private async Task<IChannel> GetPublishChannelAsync(CancellationToken cancellationToken)
    {
        if (_publishChannel is { IsOpen: true })
            return _publishChannel;

        await _publishSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_publishChannel is { IsOpen: true })
                return _publishChannel;

            var connection = await connectionManager.GetConnectionAsync(cancellationToken);
            _publishChannel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            return _publishChannel;
        }
        finally
        {
            _publishSemaphore.Release();
        }
    }
}
