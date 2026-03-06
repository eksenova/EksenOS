using Eksen.Core;

namespace Eksen.Auditing;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.Auditing);
    }

    extension(AppModules)
    {
        public static string Auditing
        {
            get { return AppModules.Eksen + ".Auditing"; }
        }
    }
}
