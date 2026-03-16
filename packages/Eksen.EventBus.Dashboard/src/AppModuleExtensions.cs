using Eksen.Core;

namespace Eksen.EventBus.Dashboard;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.EventBusDashboard);
    }

    extension(AppModules)
    {
        public static string EventBusDashboard
        {
            get { return AppModules.EventBus + ".Dashboard"; }
        }
    }
}
