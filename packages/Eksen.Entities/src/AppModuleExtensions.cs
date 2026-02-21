using Eksen.Core;

namespace Eksen.Entities;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.Entities);
    }

    extension(AppModules)
    {
        public static string Entities
        {
            get { return AppModules.Eksen + ".Entities"; }
        }
    }
}