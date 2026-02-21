using Eksen.Core;

namespace Eksen.Repositories;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.Repositories);
    }

    extension(AppModules)
    {
        public static string Repositories
        {
            get { return AppModules.Eksen + ".Repositories"; }
        }
    }
}