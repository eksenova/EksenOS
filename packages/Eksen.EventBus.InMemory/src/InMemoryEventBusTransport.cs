using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Eksen.EventBus.InMemory;

public class InMemoryEventBusTransport(
    IEventProcessor processor,
    ILogger<InMemoryEventBusTransport> logger) : IEventBusTransport
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
            Headers = headers
        };

        await _channel.Writer.WriteAsync(envelope, cancellationToken);
    }

    public async Task StartListeningAsync(CancellationToken cancellationToken = default)
    {
        _listenerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _listenerCts.Token;

        logger.LogInformation("InMemory event bus listener started");

        await foreach (var envelope in _channel.Reader.ReadAllAsync(token))
        {
            try
            {
                await processor.ProcessAsync(
                    envelope.EventTypeName,
                    envelope.Payload,
                    envelope.CorrelationId,
                    envelope.SourceApp,
                    envelope.EventId,
                    token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error processing in-memory event {EventType}", envelope.EventTypeName);
            }
        }
    }

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
}
