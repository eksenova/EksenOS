using Eksen.Core;

namespace Eksen.EventBus.InMemory;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.EventBusInMemory);
    }

    extension(AppModules)
    {
        public static string EventBusInMemory
        {
            get { return AppModules.EventBus + ".InMemory"; }
        }
    }
}
