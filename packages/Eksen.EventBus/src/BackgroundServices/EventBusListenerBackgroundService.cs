using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eksen.EventBus.BackgroundServices;

public class EventBusListenerBackgroundService(
    IEventBusTransport transport,
    ILogger<EventBusListenerBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("EventBus listener starting");

        try
        {
            await transport.StartListeningAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "EventBus listener encountered an error");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("EventBus listener stopping");
        await transport.StopListeningAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
