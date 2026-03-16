using Eksen.Core;

namespace Eksen.EventBus.EntityFrameworkCore;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.EventBusEntityFrameworkCore);
    }

    extension(AppModules)
    {
        public static string EventBusEntityFrameworkCore
        {
            get { return AppModules.EventBus + ".EntityFrameworkCore"; }
        }
    }
}
