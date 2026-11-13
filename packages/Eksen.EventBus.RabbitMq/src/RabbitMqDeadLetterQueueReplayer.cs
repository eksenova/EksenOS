using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Eksen.EventBus.RabbitMq;

public class RabbitMqDeadLetterQueueReplayer(
    IRabbitMqConnectionManager connectionManager,
    RabbitMqEventBusTransport transport,
    IEventHandlerRegistry handlerRegistry,
    IOptions<EksenEventBusOptions> eventBusOptions,
    ILogger<RabbitMqDeadLetterQueueReplayer> logger
) : IDeadLetterQueueReplayer
{
    public async Task<IReadOnlyList<DeadLetterQueueInfo>> GetQueuesAsync(CancellationToken cancellationToken = default)
    {
        var connection = await connectionManager.GetConnectionAsync(cancellationToken);
        var appName = eventBusOptions.Value.AppName;
        var result = new List<DeadLetterQueueInfo>();
        IChannel? channel = null;
        try
        {
            foreach (var descriptor in handlerRegistry.GetAllHandlers())
            {
                var queueName = RabbitMqEventBusTransport.GetQueueName(appName, descriptor.HandlerTypeName);
                var deadLetterQueueName = RabbitMqEventBusTransport.GetDeadLetterQueueName(queueName);
                channel ??= await connection.CreateChannelAsync(cancellationToken: cancellationToken);
                uint count;
                try
                {
                    var declared = await channel.QueueDeclarePassiveAsync(deadLetterQueueName, cancellationToken);
                    count = declared.MessageCount;
                }
                catch (OperationInterruptedException)
                {
                    // The dead-letter queue does not exist yet (the passive declare closed the channel).
                    await channel.DisposeAsync();
                    channel = null;
                    count = 0;
                }

                result.Add(new DeadLetterQueueInfo(
                    queueName,
                    deadLetterQueueName,
                    descriptor.EventTypeName,
                    descriptor.HandlerTypeName,
                    count));
            }
        }
        finally
        {
            if (channel != null)
            {
                await channel.DisposeAsync();
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<DeadLetteredMessage>> PeekAsync(
        string handlerQueueName,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        EnsureOwnQueue(handlerQueueName);
        var connection = await connectionManager.GetConnectionAsync(cancellationToken);
        var messages = new List<DeadLetteredMessage>();

        // Unacknowledged gets return to the queue, in their original order, when the channel closes.
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var deadLetterQueueName = RabbitMqEventBusTransport.GetDeadLetterQueueName(handlerQueueName);
        for (var i = 0; i < take; i++)
        {
            var result = await channel.BasicGetAsync(deadLetterQueueName, autoAck: false, cancellationToken);
            if (result == null)
            {
                break;
            }

            messages.Add(ToMessage(result));
        }

        return messages;
    }

    public async Task<int> ReplayAsync(
        string handlerQueueName,
        int maxMessages = 100,
        CancellationToken cancellationToken = default)
    {
        EnsureOwnQueue(handlerQueueName);
        var connection = await connectionManager.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var deadLetterQueueName = RabbitMqEventBusTransport.GetDeadLetterQueueName(handlerQueueName);
        var replayed = 0;
        while (replayed < maxMessages)
        {
            var result = await channel.BasicGetAsync(deadLetterQueueName, autoAck: false, cancellationToken);
            if (result == null)
            {
                break;
            }

            try
            {
                var properties = RabbitMqEventBusTransport.CopyProperties(result.BasicProperties, dropFailureHeaders: true);
                await transport.PublishToQueueAsync(handlerQueueName, properties, result.Body, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to replay message {MessageId} from {DeadLetterQueue}",
                    result.BasicProperties.MessageId, deadLetterQueueName);
                await channel.BasicNackAsync(result.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
                throw;
            }

            await channel.BasicAckAsync(result.DeliveryTag, multiple: false, cancellationToken);
            replayed++;
        }

        logger.LogInformation("Replayed {Count} message(s) from {DeadLetterQueue} to {Queue}",
            replayed, deadLetterQueueName, handlerQueueName);
        return replayed;
    }

    private void EnsureOwnQueue(string handlerQueueName)
    {
        var appName = eventBusOptions.Value.AppName;
        var known = handlerRegistry.GetAllHandlers()
            .Any(descriptor => RabbitMqEventBusTransport.GetQueueName(appName, descriptor.HandlerTypeName) == handlerQueueName);
        if (!known)
        {
            throw new ArgumentException($"{handlerQueueName} is not a handler queue of app {appName}.", nameof(handlerQueueName));
        }
    }

    private static DeadLetteredMessage ToMessage(BasicGetResult result)
    {
        var headers = result.BasicProperties.Headers;
        return new DeadLetteredMessage
        {
            MessageId = result.BasicProperties.MessageId,
            EventType = RabbitMqEventBusTransport.ReadString(headers, RabbitMqHeaders.EventType),
            Payload = Encoding.UTF8.GetString(result.Body.Span),
            Reason = RabbitMqEventBusTransport.ReadString(headers, RabbitMqHeaders.DeadLetterReason)
                ?? ReadDeathReason(headers),
            ExceptionType = RabbitMqEventBusTransport.ReadString(headers, RabbitMqHeaders.ExceptionType),
            ExceptionMessage = RabbitMqEventBusTransport.ReadString(headers, RabbitMqHeaders.ExceptionMessage),
            Attempts = RabbitMqEventBusTransport.ReadInt(headers, RabbitMqHeaders.Attempts),
            OriginalRoutingKey = RabbitMqEventBusTransport.ReadString(headers, RabbitMqHeaders.OriginalRoutingKey),
            FirstFailureTime = RabbitMqEventBusTransport.ReadTime(headers, RabbitMqHeaders.FirstFailureTime),
            FailedTime = RabbitMqEventBusTransport.ReadTime(headers, RabbitMqHeaders.FailedTime),
            CorrelationId = result.BasicProperties.CorrelationId,
            SourceApp = RabbitMqEventBusTransport.ReadString(headers, RabbitMqHeaders.SourceApp)
        };
    }

    /// <summary>
    /// A message the broker dead-lettered by itself (rejected, expired, overflowed) only carries <c>x-first-death-reason</c>.
    /// </summary>
    private static string? ReadDeathReason(IDictionary<string, object?>? headers)
    {
        return RabbitMqEventBusTransport.ReadString(headers, "x-first-death-reason");
    }
}
