using Eksen.Core;

namespace Eksen.EventBus;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.EventBus);
    }

    extension(AppModules)
    {
        public static string EventBus
        {
            get { return AppModules.Eksen + ".EventBus"; }
        }
    }
}
