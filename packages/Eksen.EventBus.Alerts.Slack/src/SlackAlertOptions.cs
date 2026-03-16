namespace Eksen.EventBus.Alerts.Slack;

public class SlackAlertOptions
{
    public string WebhookUrl { get; set; } = null!;

    public string? Channel { get; set; }

    public string? Username { get; set; } = "Eksen EventBus";

    public string? IconEmoji { get; set; } = ":rotating_light:";
}
