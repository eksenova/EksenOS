using System.Diagnostics;
using System.Globalization;
using System.Text;
using Eksen.EventBus.Retry;
using Eksen.EventBus.Telemetry;
using Eksen.Telemetry;
using Eksen.Telemetry.Propagation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Eksen.EventBus.RabbitMq;

/// <summary>
/// The RabbitMQ transport. Every handler of this host consumes its own durable queue <c>{AppName}.{HandlerType}</c>,
/// bound to the event's routing key and to <c>{AppName}.{routingKey}</c> (the key of a publish targeted at this app).
/// A delivery that still fails after the in-process retries is redelivered to that queue alone through a delay tier,
/// and dead-lettered to <c>{queue}.dlq</c> once its attempts are exhausted or when it is poison.
/// </summary>
public class RabbitMqEventBusTransport(
    IRabbitMqConnectionManager connectionManager,
    IEventProcessor processor,
    IEventHandlerRegistry handlerRegistry,
    IOptions<RabbitMqEventBusOptions> options,
    IOptions<EksenEventBusOptions> eventBusOptions,
    ILogger<RabbitMqEventBusTransport> logger
) : IEventBusTransport
{
    private const int PreconditionFailed = 406;
    private const int MaxExceptionMessageLength = 1024;

    private readonly List<IChannel> _consumerChannels = [];
    private readonly SemaphoreSlim _publishSemaphore = new(1, 1);
    private readonly SemaphoreSlim _delayTopologySemaphore = new(1, 1);
    private IChannel? _publishChannel;
    private bool _delayTopologyDeclared;

    public static string GetQueueName(string appName, string handlerTypeName)
    {
        return $"{appName}.{handlerTypeName}";
    }

    public static string GetDeadLetterQueueName(string queueName)
    {
        return $"{queueName}.dlq";
    }

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
        var rabbitOptions = options.Value;
        var routingKey = GetRoutingKey(eventTypeName, targetApp);

        // The outbox already stores a traceparent; direct publishes carry the caller's current span.
        headers = EventBusActivitySource.WithCurrentTraceContextIfMissing(headers);

        var properties = CreateProperties(eventId, correlationId, DateTimeOffset.UtcNow);
        properties.Headers![RabbitMqHeaders.EventType] = eventTypeName;

        if (sourceApp != null)
        {
            properties.Headers[RabbitMqHeaders.SourceApp] = sourceApp;
        }

        if (targetApp != null)
        {
            properties.Headers[RabbitMqHeaders.TargetApp] = targetApp;
        }

        if (headers != null)
        {
            foreach (var (key, value) in headers)
            {
                properties.Headers[key] = value;
            }
        }

        await PublishRawAsync(
            rabbitOptions.ExchangeName,
            routingKey,
            properties,
            Encoding.UTF8.GetBytes(payload),
            rabbitOptions.Mandatory,
            cancellationToken);

        logger.LogDebug(
            "Published event {EventType} ({EventId}) with routing key {RoutingKey}",
            eventTypeName,
            eventId,
            routingKey);
    }

    /// <summary>
    /// Redelivers an event to one handler queue of this app: immediately, or after <paramref name="delay"/> through
    /// the delay tier whose range covers it.
    /// </summary>
    public async Task RescheduleAsync(
        EventRedelivery redelivery,
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        var queueName = GetQueueName(eventBusOptions.Value.AppName, redelivery.HandlerTypeName);
        var properties = CreateProperties(redelivery.EventId, redelivery.CorrelationId, DateTimeOffset.UtcNow);
        var headers = properties.Headers!;
        headers[RabbitMqHeaders.EventType] = redelivery.EventTypeName;

        if (redelivery.SourceApp != null)
        {
            headers[RabbitMqHeaders.SourceApp] = redelivery.SourceApp;
        }

        if (redelivery.TargetApp != null)
        {
            headers[RabbitMqHeaders.TargetApp] = redelivery.TargetApp;
        }

        if (redelivery.Attempt > 1)
        {
            headers[RabbitMqHeaders.Attempt] = redelivery.Attempt;
        }

        if (redelivery.FirstFailureTime is { } firstFailure)
        {
            headers[RabbitMqHeaders.FirstFailureTime] = FormatTime(firstFailure);
        }

        // A redelivery keeps the trace of the code that requested it.
        var traceHeaders = EventBusActivitySource.WithCurrentTraceContextIfMissing(redelivery.Headers);
        if (traceHeaders != null)
        {
            foreach (var (key, value) in traceHeaders)
            {
                headers[key] = value;
            }
        }

        var body = Encoding.UTF8.GetBytes(redelivery.Payload);
        if (delay <= TimeSpan.Zero)
        {
            await PublishToQueueAsync(queueName, properties, body, cancellationToken);
        }
        else
        {
            await PublishDelayedAsync(queueName, properties, body, delay, SelectTier(delay), cancellationToken);
        }

        logger.LogDebug(
            "Event {EventType} ({EventId}) rescheduled to queue {Queue} with delay {Delay}",
            redelivery.EventTypeName,
            redelivery.EventId,
            queueName,
            delay);
    }

    public async Task StartListeningAsync(CancellationToken cancellationToken = default)
    {
        var connection = await connectionManager.GetConnectionAsync(cancellationToken);
        var rabbitOptions = options.Value;
        var appName = eventBusOptions.Value.AppName;

        await using (var setupChannel = await connection.CreateChannelAsync(cancellationToken: cancellationToken))
        {
            await DeclareExchangesAsync(setupChannel, rabbitOptions, cancellationToken);
            await DeclareDelayTopologyAsync(setupChannel, cancellationToken);
            _delayTopologyDeclared = true;
        }

        foreach (var descriptor in handlerRegistry.GetAllHandlers())
        {
            var eventTypeName = descriptor.EventTypeName;
            var queueName = GetQueueName(appName, descriptor.HandlerTypeName);
            var routingKey = GetRoutingKey(eventTypeName, targetApp: null);
            var queueOptions = GetQueueOptions(rabbitOptions, queueName);

            var consumerChannel = await DeclareHandlerQueueAsync(connection, rabbitOptions, queueName, queueOptions, cancellationToken);

            await consumerChannel.QueueBindAsync(
                queue: queueName,
                exchange: rabbitOptions.ExchangeName,
                routingKey: routingKey,
                cancellationToken: cancellationToken);

            // A publish targeted at this app carries {AppName}.{routingKey}; only this app's queues bind it.
            await consumerChannel.QueueBindAsync(
                queue: queueName,
                exchange: rabbitOptions.ExchangeName,
                routingKey: GetTargetedRoutingKey(appName, routingKey),
                cancellationToken: cancellationToken);

            if (UsesDeadLetterQueues(rabbitOptions))
            {
                var deadLetterQueueName = GetDeadLetterQueueName(queueName);
                await consumerChannel.QueueDeclareAsync(
                    queue: deadLetterQueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: cancellationToken);
                await consumerChannel.QueueBindAsync(
                    queue: deadLetterQueueName,
                    exchange: rabbitOptions.DeadLetterExchangeName!,
                    routingKey: queueName,
                    cancellationToken: cancellationToken);
            }

            var consumer = new AsyncEventingBasicConsumer(consumerChannel);
            var boundDescriptor = descriptor;
            var boundQueueName = queueName;
            consumer.ReceivedAsync += (_, ea) =>
                HandleDeliveryAsync(consumerChannel, ea, boundQueueName, boundDescriptor, cancellationToken);

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

    internal async Task HandleDeliveryAsync(
        IChannel consumerChannel,
        BasicDeliverEventArgs ea,
        string queueName,
        EventHandlerDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        using var activity = StartProcessActivity(ea, queueName);
        var properties = ea.BasicProperties;
        var busOptions = eventBusOptions.Value;
        var attempt = Math.Max(1, ReadInt(properties.Headers, RabbitMqHeaders.Attempt) ?? 1);
        var maxRedeliveries = Math.Max(0, busOptions.DeadLetter.MaxRetryAttemptsBeforeDeadLetter);
        var canRedeliver = busOptions.Redelivery.IsEnabled && attempt <= maxRedeliveries;
        var firstFailure = ReadTime(properties.Headers, RabbitMqHeaders.FirstFailureTime);
        var eventType = ReadString(properties.Headers, RabbitMqHeaders.EventType) ?? descriptor.EventTypeName;
        activity?.SetTag(EksenSemanticConventions.EksenEventType, eventType);

        Exception failure;
        string? deadLetterReason = null;
        try
        {
            if (!Guid.TryParse(properties.MessageId, out var eventId))
            {
                throw new PoisonMessageException(
                    $"Message on queue {queueName} has no event id (message id '{properties.MessageId}').");
            }

            var delivery = new EventDelivery
            {
                EventTypeName = eventType,
                Payload = Encoding.UTF8.GetString(ea.Body.Span),
                EventId = eventId,
                HandlerTypeName = descriptor.HandlerTypeName,
                CorrelationId = properties.CorrelationId,
                SourceApp = ReadString(properties.Headers, RabbitMqHeaders.SourceApp),
                TargetApp = ReadString(properties.Headers, RabbitMqHeaders.TargetApp),
                Attempt = attempt,
                FirstFailureTime = firstFailure,
                IsFinalAttempt = !canRedeliver
            };

            // The consumer span is Activity.Current here, so the event processor's handler spans are its children.
            await processor.ProcessAsync(delivery, cancellationToken);
            await consumerChannel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken);
            return;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down: hand the message back for the next consumer.
            await consumerChannel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
            return;
        }
        catch (PoisonMessageException poison)
        {
            failure = poison;
            deadLetterReason = RabbitMqDeadLetterReasons.Poison;
        }
        catch (Exception exception)
        {
            failure = exception;
            if (!canRedeliver)
            {
                deadLetterReason = busOptions.Redelivery.IsEnabled
                    ? RabbitMqDeadLetterReasons.MaxAttemptsExceeded
                    : RabbitMqDeadLetterReasons.Failed;
            }
        }

        activity.RecordException(failure);
        firstFailure ??= DateTime.UtcNow;

        if (deadLetterReason == null)
        {
            try
            {
                var redeliveryOptions = busOptions.Redelivery;
                var delay = ExponentialBackoff.GetDelay(
                    attempt,
                    redeliveryOptions.InitialDelay,
                    redeliveryOptions.MaxDelay,
                    redeliveryOptions.BackoffMultiplier,
                    redeliveryOptions.JitterFactor);
                var redelivered = CopyForRetry(ea, attempt + 1, firstFailure.Value);
                await PublishDelayedAsync(queueName, redelivered, ea.Body, delay, Math.Min(attempt, TierCount), CancellationToken.None);
                await consumerChannel.BasicAckAsync(ea.DeliveryTag, multiple: false, CancellationToken.None);
                activity.SetOutcome("redelivery_scheduled");
                logger.LogWarning(
                    failure,
                    "Message {MessageId} on queue {Queue} failed on attempt {Attempt}; redelivering in {Delay}",
                    properties.MessageId,
                    queueName,
                    attempt,
                    delay);
                return;
            }
            catch (Exception redeliveryError)
            {
                logger.LogError(redeliveryError, "Failed to schedule the redelivery of message {MessageId} on queue {Queue}",
                    properties.MessageId, queueName);
                deadLetterReason = RabbitMqDeadLetterReasons.RedeliveryFailed;
            }
        }

        await DeadLetterAsync(consumerChannel, ea, queueName, descriptor, deadLetterReason, failure, attempt, firstFailure.Value);
        activity.SetOutcome("dead_lettered");
    }

    private async Task DeadLetterAsync(
        IChannel consumerChannel,
        BasicDeliverEventArgs ea,
        string queueName,
        EventHandlerDescriptor descriptor,
        string reason,
        Exception failure,
        int attempt,
        DateTime firstFailure)
    {
        var rabbitOptions = options.Value;
        logger.LogError(
            failure,
            "Message {MessageId} on queue {Queue} is dead-lettered ({Reason}) after {Attempts} attempt(s)",
            ea.BasicProperties.MessageId,
            queueName,
            reason,
            attempt);

        if (!UsesDeadLetterQueues(rabbitOptions))
        {
            await consumerChannel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);
            return;
        }

        try
        {
            var properties = CopyProperties(ea.BasicProperties, dropFailureHeaders: true);
            var headers = properties.Headers!;
            var exceptionMessage = failure.Message.Length <= MaxExceptionMessageLength
                ? failure.Message
                : failure.Message[..MaxExceptionMessageLength];
            headers[RabbitMqHeaders.DeadLetterReason] = reason;
            headers[RabbitMqHeaders.ExceptionType] = failure.GetType().FullName ?? failure.GetType().Name;
            headers[RabbitMqHeaders.ExceptionMessage] = exceptionMessage;
            headers[RabbitMqHeaders.Attempts] = attempt;
            headers[RabbitMqHeaders.OriginalRoutingKey] =
                ReadString(ea.BasicProperties.Headers, RabbitMqHeaders.OriginalRoutingKey) ?? ea.RoutingKey;
            headers[RabbitMqHeaders.FirstFailureTime] = FormatTime(firstFailure);
            headers[RabbitMqHeaders.FailedTime] = FormatTime(DateTime.UtcNow);
            headers[RabbitMqHeaders.Queue] = queueName;
            headers[RabbitMqHeaders.Handler] = descriptor.HandlerTypeName;

            await PublishRawAsync(
                rabbitOptions.DeadLetterExchangeName!,
                queueName,
                properties,
                ea.Body,
                mandatory: true,
                CancellationToken.None);
            await consumerChannel.BasicAckAsync(ea.DeliveryTag, multiple: false, CancellationToken.None);
        }
        catch (Exception publishError)
        {
            // The queue's own dead-letter arguments route the rejected message to its dead-letter queue.
            logger.LogError(publishError, "Failed to publish message {MessageId} to the dead-letter queue of {Queue}; rejecting it",
                ea.BasicProperties.MessageId, queueName);
            await consumerChannel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);
        }
    }

    internal static Activity? StartProcessActivity(BasicDeliverEventArgs ea, string queueName)
    {
        var source = RabbitMqTelemetry.ActivitySource;
        if (!source.HasListeners())
        {
            return null;
        }

        var properties = ea.BasicProperties;
        var parent = TraceContextPropagation.ExtractFromAmqp(properties.Headers, out _);
        var tags = new List<KeyValuePair<string, object?>>
        {
            new(EksenSemanticConventions.MessagingSystem, RabbitMqTelemetry.MessagingSystemName),
            new(EksenSemanticConventions.MessagingOperationType, "process"),
            new(EksenSemanticConventions.MessagingOperationName, "deliver"),
            new(EksenSemanticConventions.MessagingDestinationName, queueName),
            new(EksenSemanticConventions.MessagingRabbitMqDestinationRoutingKey, ea.RoutingKey)
        };

        if (!string.IsNullOrEmpty(properties.MessageId))
        {
            tags.Add(new(EksenSemanticConventions.MessagingMessageId, properties.MessageId));
        }

        if (!string.IsNullOrEmpty(properties.CorrelationId))
        {
            tags.Add(new(EksenSemanticConventions.MessagingMessageConversationId, properties.CorrelationId));
        }

        if (ReadString(properties.Headers, RabbitMqHeaders.SourceApp) is { } sourceApp)
        {
            tags.Add(new(EksenSemanticConventions.EksenSourceApp, sourceApp));
        }

        return EventBusActivitySource.StartConsumer(source, $"process {ea.RoutingKey}", parent, tags);
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

    /// <summary>
    /// Publishes straight to a queue through the default exchange.
    /// </summary>
    internal Task PublishToQueueAsync(
        string queueName,
        BasicProperties properties,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        return PublishRawAsync(string.Empty, queueName, properties, body, mandatory: true, cancellationToken);
    }

    /// <summary>
    /// Parks a message in delay tier <paramref name="tier"/> for <paramref name="delay"/>; when it expires the tier
    /// queue dead-letters it through the default exchange with its routing key, the target queue's name, so it returns
    /// to that queue only.
    /// </summary>
    internal async Task PublishDelayedAsync(
        string queueName,
        BasicProperties properties,
        ReadOnlyMemory<byte> body,
        TimeSpan delay,
        int tier,
        CancellationToken cancellationToken)
    {
        await EnsureDelayTopologyAsync(cancellationToken);

        properties.Headers ??= new Dictionary<string, object?>();
        properties.Headers[RabbitMqHeaders.DelayTier] = tier.ToString(CultureInfo.InvariantCulture);
        properties.Expiration = ((long)Math.Ceiling(Math.Max(0, delay.TotalMilliseconds))).ToString(CultureInfo.InvariantCulture);

        await PublishRawAsync(GetDelayExchangeName(), queueName, properties, body, mandatory: true, cancellationToken);
    }

    internal async Task PublishRawAsync(
        string exchange,
        string routingKey,
        BasicProperties properties,
        ReadOnlyMemory<byte> body,
        bool mandatory,
        CancellationToken cancellationToken)
    {
        var channel = await GetPublishChannelAsync(cancellationToken);

        // With publisher confirms the call completes once the broker confirmed the message, and throws a
        // PublishException when it nacked it or (mandatory) could not route it.
        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: mandatory,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    internal int TierCount
    {
        get { return Math.Max(1, eventBusOptions.Value.DeadLetter.MaxRetryAttemptsBeforeDeadLetter); }
    }

    internal string GetDelayExchangeName()
    {
        var rabbitOptions = options.Value;
        return rabbitOptions.DelayExchangeName ?? $"{rabbitOptions.ExchangeName}.delay";
    }

    internal string GetDelayQueueName(int tier)
    {
        return $"{options.Value.ExchangeName}.delay.{tier}";
    }

    /// <summary>
    /// The smallest tier whose (jittered) delay range covers <paramref name="delay"/>, so messages that share a tier
    /// queue expire close together.
    /// </summary>
    internal int SelectTier(TimeSpan delay)
    {
        var redelivery = eventBusOptions.Value.Redelivery;
        for (var tier = 1; tier <= TierCount; tier++)
        {
            var nominal = ExponentialBackoff.GetNominalDelay(tier, redelivery.InitialDelay, redelivery.MaxDelay, redelivery.BackoffMultiplier);
            if (delay.TotalMilliseconds <= nominal.TotalMilliseconds * (1 + Math.Clamp(redelivery.JitterFactor, 0, 1)))
            {
                return tier;
            }
        }

        return TierCount;
    }

    internal static BasicProperties CopyProperties(IReadOnlyBasicProperties source, bool dropFailureHeaders)
    {
        var headers = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (source.Headers != null)
        {
            foreach (var (key, value) in source.Headers)
            {
                if (dropFailureHeaders && RabbitMqHeaders.FailureHeaders.Contains(key, StringComparer.Ordinal))
                {
                    continue;
                }

                headers[key] = value;
            }
        }

        var properties = new BasicProperties
        {
            MessageId = source.MessageId,
            CorrelationId = source.CorrelationId,
            ContentType = source.ContentType ?? "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Headers = headers
        };

        if (source.IsTimestampPresent())
        {
            properties.Timestamp = source.Timestamp;
        }

        return properties;
    }

    private BasicProperties CopyForRetry(BasicDeliverEventArgs ea, int nextAttempt, DateTime firstFailure)
    {
        var properties = CopyProperties(ea.BasicProperties, dropFailureHeaders: true);
        var headers = properties.Headers!;
        headers[RabbitMqHeaders.Attempt] = nextAttempt;
        headers[RabbitMqHeaders.FirstFailureTime] = FormatTime(firstFailure);
        headers[RabbitMqHeaders.OriginalRoutingKey] =
            ReadString(ea.BasicProperties.Headers, RabbitMqHeaders.OriginalRoutingKey) ?? ea.RoutingKey;
        return properties;
    }

    private static BasicProperties CreateProperties(Guid eventId, string? correlationId, DateTimeOffset timestamp)
    {
        return new BasicProperties
        {
            MessageId = eventId.ToString(),
            CorrelationId = correlationId,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = new AmqpTimestamp(timestamp.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>(StringComparer.Ordinal)
        };
    }

    private async Task<IChannel> DeclareHandlerQueueAsync(
        IConnection connection,
        RabbitMqEventBusOptions rabbitOptions,
        string queueName,
        QueueOptions queueOptions,
        CancellationToken cancellationToken)
    {
        var routeToOwnDeadLetterQueue = UsesDeadLetterQueues(rabbitOptions) && queueOptions.DeadLetterRoutingKey == null;
        var channel = await CreateConsumerChannelAsync(connection, rabbitOptions, cancellationToken);
        try
        {
            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: queueOptions.Durable,
                exclusive: queueOptions.Exclusive,
                autoDelete: queueOptions.AutoDelete,
                arguments: BuildQueueArguments(rabbitOptions, queueOptions, routeToOwnDeadLetterQueue ? queueName : null),
                cancellationToken: cancellationToken);
            return channel;
        }
        catch (OperationInterruptedException exception)
            when (routeToOwnDeadLetterQueue && exception.ShutdownReason?.ReplyCode == PreconditionFailed)
        {
            // The queue predates per-queue dead-letter routing and its arguments cannot change in place. Keep using it;
            // explicit dead-lettering still reaches its dead-letter queue, but messages the broker itself rejects or
            // expires from it follow the old arguments until the queue is recreated.
            logger.LogWarning(
                "Queue {Queue} was declared without a dead-letter routing key; recreate it to route broker dead letters to {DeadLetterQueue}",
                queueName,
                GetDeadLetterQueueName(queueName));
            await channel.DisposeAsync();

            channel = await CreateConsumerChannelAsync(connection, rabbitOptions, cancellationToken);
            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: queueOptions.Durable,
                exclusive: queueOptions.Exclusive,
                autoDelete: queueOptions.AutoDelete,
                arguments: BuildQueueArguments(rabbitOptions, queueOptions, deadLetterRoutingKey: null),
                cancellationToken: cancellationToken);
            return channel;
        }
    }

    private static async Task<IChannel> CreateConsumerChannelAsync(
        IConnection connection,
        RabbitMqEventBusOptions rabbitOptions,
        CancellationToken cancellationToken)
    {
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: rabbitOptions.PrefetchCount,
            global: false,
            cancellationToken: cancellationToken);
        return channel;
    }

    private static async Task DeclareExchangesAsync(
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

    private async Task DeclareDelayTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        var delayExchange = GetDelayExchangeName();
        await channel.ExchangeDeclareAsync(
            exchange: delayExchange,
            type: ExchangeType.Headers,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        for (var tier = 1; tier <= TierCount; tier++)
        {
            var tierQueue = GetDelayQueueName(tier);
            await channel.QueueDeclareAsync(
                queue: tierQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    // Expired messages go back through the default exchange to the queue named by their routing key.
                    ["x-dead-letter-exchange"] = string.Empty
                },
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: tierQueue,
                exchange: delayExchange,
                routingKey: string.Empty,
                arguments: new Dictionary<string, object?>
                {
                    ["x-match"] = "all",
                    [RabbitMqHeaders.DelayTier] = tier.ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken: cancellationToken);
        }
    }

    private async Task EnsureDelayTopologyAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _delayTopologyDeclared))
        {
            return;
        }

        await _delayTopologySemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_delayTopologyDeclared)
            {
                return;
            }

            var connection = await connectionManager.GetConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await DeclareDelayTopologyAsync(channel, cancellationToken);
            Volatile.Write(ref _delayTopologyDeclared, true);
        }
        finally
        {
            _delayTopologySemaphore.Release();
        }
    }

    private static bool UsesDeadLetterQueues(RabbitMqEventBusOptions rabbitOptions)
    {
        return rabbitOptions.DeclareDeadLetterQueues && rabbitOptions.DeadLetterExchangeName != null;
    }

    private string GetRoutingKey(string eventTypeName, string? targetApp)
    {
        var rabbitOptions = options.Value;
        var routingKey = rabbitOptions.EventQueueBindings.TryGetValue(eventTypeName, out var binding)
            ? binding.RoutingKey
            : eventTypeName;

        return targetApp != null ? GetTargetedRoutingKey(targetApp, routingKey) : routingKey;
    }

    private static string GetTargetedRoutingKey(string appName, string routingKey)
    {
        return $"{appName}.{routingKey}";
    }

    private static QueueOptions GetQueueOptions(RabbitMqEventBusOptions rabbitOptions, string queueName)
    {
        return rabbitOptions.QueueConfigurations.TryGetValue(queueName, out var queueOptions)
            ? queueOptions
            : new QueueOptions();
    }

    internal static Dictionary<string, object?> BuildQueueArguments(
        RabbitMqEventBusOptions rabbitOptions,
        QueueOptions queueOptions,
        string? deadLetterRoutingKey)
    {
        var arguments = new Dictionary<string, object?>();
        var deadLetterExchange = queueOptions.DeadLetterExchange ?? rabbitOptions.DeadLetterExchangeName;
        if (deadLetterExchange != null)
        {
            arguments["x-dead-letter-exchange"] = deadLetterExchange;
        }

        var routingKey = queueOptions.DeadLetterRoutingKey ?? deadLetterRoutingKey;
        if (routingKey != null)
        {
            arguments["x-dead-letter-routing-key"] = routingKey;
        }

        if (queueOptions.MessageTtl.HasValue)
        {
            arguments["x-message-ttl"] = queueOptions.MessageTtl.Value;
        }

        if (queueOptions.MaxLength.HasValue)
        {
            arguments["x-max-length"] = queueOptions.MaxLength.Value;
        }

        if (queueOptions.Arguments != null)
        {
            foreach (var (key, value) in queueOptions.Arguments)
            {
                arguments[key] = value;
            }
        }

        return arguments;
    }

    private async Task<IChannel> GetPublishChannelAsync(CancellationToken cancellationToken)
    {
        var current = _publishChannel;
        if (current is { IsOpen: true })
        {
            return current;
        }

        await _publishSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_publishChannel is { IsOpen: true })
            {
                return _publishChannel;
            }

            if (_publishChannel != null)
            {
                await _publishChannel.DisposeAsync();
            }

            var rabbitOptions = options.Value;
            var connection = await connectionManager.GetConnectionAsync(cancellationToken);
            var channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: rabbitOptions.PublisherConfirms,
                    publisherConfirmationTrackingEnabled: rabbitOptions.PublisherConfirms),
                cancellationToken);

            // A host that only publishes never ran the listener's setup; publishing to a missing exchange would close the channel.
            await DeclareExchangesAsync(channel, rabbitOptions, cancellationToken);
            _publishChannel = channel;
            return channel;
        }
        finally
        {
            _publishSemaphore.Release();
        }
    }

    internal static string? ReadString(IDictionary<string, object?>? headers, string key)
    {
        if (headers == null || !headers.TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
            string text => text,
            null => null,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    internal static int? ReadInt(IDictionary<string, object?>? headers, string key)
    {
        if (headers == null || !headers.TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            int number => number,
            long number => (int)number,
            short number => number,
            byte number => number,
            sbyte number => number,
            uint number => (int)number,
            _ => int.TryParse(ReadString(headers, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null
        };
    }

    internal static DateTime? ReadTime(IDictionary<string, object?>? headers, string key)
    {
        var text = ReadString(headers, key);
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var time)
            ? time
            : null;
    }

    internal static string FormatTime(DateTime time)
    {
        return DateTime.SpecifyKind(time.ToUniversalTime(), DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
    }
}
