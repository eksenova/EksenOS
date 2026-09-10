using Eksen.Core;

namespace Eksen.Cloudflare.Redis;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.CloudflareRedis);
    }

    extension(AppModules)
    {
        public static string CloudflareRedis
        {
            get { return AppModules.Eksen + ".Cloudflare.Redis"; }
        }
    }
}
