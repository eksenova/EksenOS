using Eksen.Core;

namespace Eksen.Localization;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.Localization);
    }

    extension(AppModules)
    {
        public static string Localization
        {
            get { return AppModules.Eksen + ".Localization"; }
        }
    }
}