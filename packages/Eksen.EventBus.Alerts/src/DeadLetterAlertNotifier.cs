using Eksen.EventBus.DeadLetter;

namespace Eksen.EventBus.Alerts;

public class DeadLetterAlertNotifier(IDeadLetterAlertManager alertManager) : IDeadLetterNotifier
{
    public Task NotifyAsync(DeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        return alertManager.SendAlertAsync(message, cancellationToken);
    }
}
