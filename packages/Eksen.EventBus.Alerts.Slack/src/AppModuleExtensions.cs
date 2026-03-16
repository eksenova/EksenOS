using Eksen.Core;

namespace Eksen.EventBus.Alerts.Slack;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.EventBusAlertsSlack);
    }

    extension(AppModules)
    {
        public static string EventBusAlertsSlack
        {
            get { return AppModules.EventBusAlerts + ".Slack"; }
        }
    }
}
