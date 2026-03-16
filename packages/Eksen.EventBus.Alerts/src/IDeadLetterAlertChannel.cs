using Eksen.EventBus.DeadLetter;

namespace Eksen.EventBus.Alerts;

public interface IDeadLetterAlertChannel
{
    string Name { get; }

    Task SendAlertAsync(DeadLetterAlert alert, CancellationToken cancellationToken = default);
}

public class DeadLetterAlert
{
    public required DeadLetterMessage Message { get; init; }

    public required string AppName { get; init; }

    public DateTime AlertTime { get; init; } = DateTime.UtcNow;

    public string Summary => $"Event '{Message.EventType}' failed after {Message.TotalRetryCount} retries. " +
                             $"Handler: {Message.HandlerType ?? "N/A"}. Error: {Message.LastError}";
}
