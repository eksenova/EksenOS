using Eksen.Core;

namespace Eksen.Telemetry;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.Telemetry);
    }

    extension(AppModules)
    {
        public static string Telemetry
        {
            get { return AppModules.Eksen + ".Telemetry"; }
        }
    }
}
