namespace Eksen.EventBus.Alerts;

public class EksenEventBusAlertOptions
{
    public bool IsEnabled { get; set; } = true;

    public List<string> EnabledChannels { get; set; } = [];
}
