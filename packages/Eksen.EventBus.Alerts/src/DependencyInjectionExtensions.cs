using Eksen.EventBus;
using Eksen.EventBus.Alerts;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

public interface IEksenEventBusAlertsBuilder
{
    IEksenEventBusBuilder EventBusBuilder { get; }

    IEksenEventBusAlertsBuilder Configure(Action<EksenEventBusAlertOptions> configureOptions);
}

public class EksenEventBusAlertsBuilder(IEksenEventBusBuilder eventBusBuilder) : IEksenEventBusAlertsBuilder
{
    public IEksenEventBusBuilder EventBusBuilder { get; } = eventBusBuilder;

    public IEksenEventBusAlertsBuilder Configure(Action<EksenEventBusAlertOptions> configureOptions)
    {
        EventBusBuilder.EksenBuilder.Services.Configure(configureOptions);
        return this;
    }
}

public static class AlertsDependencyInjectionExtensions
{
    public static IEksenEventBusBuilder AddAlerts(
        this IEksenEventBusBuilder builder,
        Action<IEksenEventBusAlertsBuilder>? configureAction = null)
    {
        var services = builder.EksenBuilder.Services;

        services.AddSingleton<IDeadLetterAlertManager, DeadLetterAlertManager>();
        services.AddSingleton<IDeadLetterNotifier, DeadLetterAlertNotifier>();

        if (configureAction != null)
        {
            var alertsBuilder = new EksenEventBusAlertsBuilder(builder);
            configureAction(alertsBuilder);
        }

        return builder;
    }
}
