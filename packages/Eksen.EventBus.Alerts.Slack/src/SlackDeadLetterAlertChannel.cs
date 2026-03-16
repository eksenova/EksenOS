using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.EventBus.Alerts.Slack;

public class SlackDeadLetterAlertChannel(
    IHttpClientFactory httpClientFactory,
    IOptions<SlackAlertOptions> options,
    ILogger<SlackDeadLetterAlertChannel> logger) : IDeadLetterAlertChannel
{
    public string Name => "Slack";

    public async Task SendAlertAsync(DeadLetterAlert alert, CancellationToken cancellationToken = default)
    {
        var slackOptions = options.Value;
        var client = httpClientFactory.CreateClient("EksenEventBusSlack");

        var message = alert.Message;

        var payload = new
        {
            channel = slackOptions.Channel,
            username = slackOptions.Username,
            icon_emoji = slackOptions.IconEmoji,
            blocks = new object[]
            {
                new
                {
                    type = "header",
                    text = new { type = "plain_text", text = ":rotating_light: Dead Letter Alert", emoji = true }
                },
                new
                {
                    type = "section",
                    fields = new object[]
                    {
                        new { type = "mrkdwn", text = $"*App:*\n{alert.AppName}" },
                        new { type = "mrkdwn", text = $"*Event Type:*\n`{message.EventType}`" },
                        new { type = "mrkdwn", text = $"*Handler:*\n`{message.HandlerType ?? "N/A"}`" },
                        new { type = "mrkdwn", text = $"*Retries:*\n{message.TotalRetryCount}" },
                        new { type = "mrkdwn", text = $"*Source App:*\n{message.SourceApp ?? "N/A"}" },
                        new { type = "mrkdwn", text = $"*Correlation ID:*\n{message.CorrelationId ?? "N/A"}" },
                    }
                },
                new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text = $"*Error:*\n```{Truncate(message.LastError, 500)}```" }
                },
                new
                {
                    type = "context",
                    elements = new object[]
                    {
                        new
                        {
                            type = "mrkdwn",
                            text = $"Event ID: `{message.OriginalMessageId}` | Failed at: {message.FailedTime:u}"
                        }
                    }
                }
            }
        };

        var response = await client.PostAsJsonAsync(slackOptions.WebhookUrl, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "Failed to send Slack alert. Status: {Status}, Body: {Body}",
                response.StatusCode,
                body);
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
