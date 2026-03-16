namespace Eksen.EventBus;

public class PublishOptions
{
    public string? TargetApp { get; set; }

    public string? CorrelationId { get; set; }

    public TimeSpan? Delay { get; set; }

    public Dictionary<string, string>? Headers { get; set; }

    public EventDispatchMode DispatchMode { get; set; } = EventDispatchMode.Immediate;
}
