using Eksen.EventBus.DeadLetter;

namespace Eksen.EventBus;

public interface IDeadLetterNotifier
{
    Task NotifyAsync(DeadLetterMessage message, CancellationToken cancellationToken = default);
}
