using Eksen.EventBus.DeadLetter;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.EventBus.Alerts;

public interface IDeadLetterAlertManager
{
    Task SendAlertAsync(DeadLetterMessage message, CancellationToken cancellationToken = default);
}

public class DeadLetterAlertManager(
    IEnumerable<IDeadLetterAlertChannel> channels,
    IOptions<EksenEventBusAlertOptions> alertOptions,
    IOptions<EksenEventBusOptions> eventBusOptions,
    ILogger<DeadLetterAlertManager> logger) : IDeadLetterAlertManager
{
    public async Task SendAlertAsync(DeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        var options = alertOptions.Value;

        if (!options.IsEnabled)
            return;

        var alert = new DeadLetterAlert
        {
            Message = message,
            AppName = eventBusOptions.Value.AppName
        };

        var activeChannels = channels
            .Where(c => options.EnabledChannels.Count == 0 || options.EnabledChannels.Contains(c.Name))
            .ToList();

        foreach (var channel in activeChannels)
        {
            try
            {
                await channel.SendAlertAsync(alert, cancellationToken);
                logger.LogInformation(
                    "Dead letter alert sent via {Channel} for event {EventType}",
                    channel.Name,
                    message.EventType);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to send dead letter alert via {Channel} for event {EventType}",
                    channel.Name,
                    message.EventType);
            }
        }
    }
}
