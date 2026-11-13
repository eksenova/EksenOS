using Eksen.Core;

namespace Eksen.EventBus.Redis;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.EventBusRedis);
    }

    extension(AppModules)
    {
        public static string EventBusRedis
        {
            get { return AppModules.EventBus + ".Redis"; }
        }
    }
}
