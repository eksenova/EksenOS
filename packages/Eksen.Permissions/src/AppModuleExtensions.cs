using Eksen.Core;

namespace Eksen.Permissions;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.Permissions);
    }

    extension(AppModules)
    {
        public static string Permissions
        {
            get { return AppModules.Eksen + ".Permissions"; }
        }
    }
}