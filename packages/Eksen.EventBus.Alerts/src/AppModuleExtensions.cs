using Eksen.Core;

namespace Eksen.EventBus.Alerts;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.EventBusAlerts);
    }

    extension(AppModules)
    {
        public static string EventBusAlerts
        {
            get { return AppModules.EventBus + ".Alerts"; }
        }
    }
}
