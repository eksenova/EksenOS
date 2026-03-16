using Eksen.Core;

namespace Eksen.EventBus.RabbitMq;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.EventBusRabbitMq);
    }

    extension(AppModules)
    {
        public static string EventBusRabbitMq
        {
            get { return AppModules.EventBus + ".RabbitMq"; }
        }
    }
}
