using System.Diagnostics;
using System.Threading.Channels;
using Eksen.EventBus.Telemetry;
using Eksen.Telemetry;
using Eksen.Telemetry.Propagation;
using Microsoft.Extensions.Logging;

namespace Eksen.EventBus.InMemory;

public class InMemoryEventBusTransport(
    IEventProcessor processor,
    ILogger<InMemoryEventBusTransport> logger
) : IEventBusTransport, IEventRescheduler
{
    private readonly Channel<InMemoryEnvelope> _channel = Channel.CreateUnbounded<InMemoryEnvelope>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    private CancellationTokenSource? _listenerCts;

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
        var envelope = new InMemoryEnvelope
        {
            EventTypeName = eventTypeName,
            Payload = payload,
            CorrelationId = correlationId,
            SourceApp = sourceApp,
            TargetApp = targetApp,
            EventId = eventId,
            // The channel hop loses the ambient span, so it travels in the headers like on a real broker.
            Headers = EventBusActivitySource.WithCurrentTraceContextIfMissing(headers)
        };

        await _channel.Writer.WriteAsync(envelope, cancellationToken);
    }

    /// <summary>
    /// Delivers the event to the one handler named by the redelivery after <paramref name="delay"/>.
    /// </summary>
    public async Task RescheduleAsync(
        EventRedelivery redelivery,
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        var envelope = new InMemoryEnvelope
        {
            EventTypeName = redelivery.EventTypeName,
            Payload = redelivery.Payload,
            CorrelationId = redelivery.CorrelationId,
            SourceApp = redelivery.SourceApp,
            TargetApp = redelivery.TargetApp,
            EventId = redelivery.EventId,
            HandlerTypeName = redelivery.HandlerTypeName,
            Attempt = redelivery.Attempt,
            Headers = EventBusActivitySource.WithCurrentTraceContextIfMissing(redelivery.Headers)
        };

        if (delay <= TimeSpan.Zero)
        {
            await _channel.Writer.WriteAsync(envelope, cancellationToken);
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cancellationToken);
                await _channel.Writer.WriteAsync(envelope, cancellationToken);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ChannelClosedException)
            {
                logger.LogWarning("Delayed in-memory redelivery of event {EventType} was dropped: the bus stopped", envelope.EventTypeName);
            }
        }, CancellationToken.None);
    }

    public async Task StartListeningAsync(CancellationToken cancellationToken = default)
    {
        _listenerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _listenerCts.Token;

        logger.LogInformation("InMemory event bus listener started");

        await foreach (var envelope in _channel.Reader.ReadAllAsync(token))
        {
            using var activity = StartProcessActivity(envelope);
            try
            {
                if (envelope.HandlerTypeName != null)
                {
                    await processor.ProcessAsync(
                        new EventDelivery
                        {
                            EventTypeName = envelope.EventTypeName,
                            Payload = envelope.Payload,
                            EventId = envelope.EventId,
                            HandlerTypeName = envelope.HandlerTypeName,
                            CorrelationId = envelope.CorrelationId,
                            SourceApp = envelope.SourceApp,
                            TargetApp = envelope.TargetApp,
                            Attempt = envelope.Attempt
                        },
                        token);
                }
                else
                {
                    await processor.ProcessAsync(
                        envelope.EventTypeName,
                        envelope.Payload,
                        envelope.CorrelationId,
                        envelope.SourceApp,
                        envelope.EventId,
                        token);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                activity.RecordException(ex);
                logger.LogError(ex, "Error processing in-memory event {EventType}", envelope.EventTypeName);
            }
        }
    }

    internal static Activity? StartProcessActivity(InMemoryEnvelope envelope)
    {
        var source = EventBusActivitySource.Instance;
        if (!source.HasListeners())
        {
            return null;
        }

        var parent = TraceContextPropagation.Extract(envelope.Headers, out _);
        var tags = new List<KeyValuePair<string, object?>>
        {
            new(EksenSemanticConventions.MessagingSystem, InMemoryMessagingSystemName),
            new(EksenSemanticConventions.MessagingOperationType, "process"),
            new(EksenSemanticConventions.MessagingDestinationName, envelope.TargetApp ?? envelope.EventTypeName),
            new(EksenSemanticConventions.MessagingMessageId, envelope.EventId.ToString()),
            new(EksenSemanticConventions.EksenEventType, envelope.EventTypeName)
        };

        if (envelope.CorrelationId is { Length: > 0 } correlationId)
        {
            tags.Add(new(EksenSemanticConventions.MessagingMessageConversationId, correlationId));
        }

        if (envelope.SourceApp is { Length: > 0 } sourceApp)
        {
            tags.Add(new(EksenSemanticConventions.EksenSourceApp, sourceApp));
        }

        return EventBusActivitySource.StartConsumer(source, $"process {envelope.EventTypeName}", parent, tags);
    }

    internal const string InMemoryMessagingSystemName = "in_memory";

    public Task StopListeningAsync(CancellationToken cancellationToken = default)
    {
        _listenerCts?.Cancel();
        _channel.Writer.Complete();
        logger.LogInformation("InMemory event bus listener stopped");
        return Task.CompletedTask;
    }
}

internal class InMemoryEnvelope
{
    public required string EventTypeName { get; init; }
    public required string Payload { get; init; }
    public string? CorrelationId { get; init; }
    public string? SourceApp { get; init; }
    public string? TargetApp { get; init; }
    public required Guid EventId { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public string? HandlerTypeName { get; init; }
    public int Attempt { get; init; } = 1;
}
