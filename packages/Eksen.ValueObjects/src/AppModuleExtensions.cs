using Eksen.Core;

namespace Eksen.ValueObjects;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.ValueObjects);
    }

    extension(AppModules)
    {
        public static string ValueObjects
        {
            get { return AppModules.Eksen + ".ValueObjects"; }
        }
    }
}