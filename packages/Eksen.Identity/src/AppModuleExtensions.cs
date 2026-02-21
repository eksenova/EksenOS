using Eksen.Core;

namespace Eksen.Identity;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.Identity);
    }

    extension(AppModules)
    {
        public static string Identity
        {
            get { return AppModules.Eksen + ".Identity"; }
        }
    }
}