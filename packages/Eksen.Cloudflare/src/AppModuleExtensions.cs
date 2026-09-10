using Eksen.Core;

namespace Eksen.Cloudflare;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.Cloudflare);
    }

    extension(AppModules)
    {
        public static string Cloudflare
        {
            get { return AppModules.Eksen + ".Cloudflare"; }
        }
    }
}
