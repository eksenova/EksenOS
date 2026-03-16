namespace Eksen.EventBus;

public interface IEventBusTransport
{
    Task PublishAsync(
        string eventTypeName,
        string payload,
        string? correlationId,
        string? sourceApp,
        string? targetApp,
        Guid eventId,
        Dictionary<string, string>? headers,
        CancellationToken cancellationToken = default);

    Task StartListeningAsync(CancellationToken cancellationToken = default);

    Task StopListeningAsync(CancellationToken cancellationToken = default);
}
