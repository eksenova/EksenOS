using Eksen.EventBus.Alerts;
using Eksen.EventBus.Alerts.Slack;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

public static class SlackAlertsDependencyInjectionExtensions
{
    public static IEksenEventBusAlertsBuilder UseSlack(
        this IEksenEventBusAlertsBuilder builder,
        Action<SlackAlertOptions> configureOptions)
    {
        var services = builder.EventBusBuilder.EksenBuilder.Services;

        services.Configure(configureOptions);
        services.AddHttpClient("EksenEventBusSlack");
        services.AddSingleton<IDeadLetterAlertChannel, SlackDeadLetterAlertChannel>();

        return builder;
    }
}
