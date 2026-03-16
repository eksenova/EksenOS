using Eksen.Core;

namespace Eksen.DistributedLocks;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.DistributedLocks);
    }

    extension(AppModules)
    {
        public static string DistributedLocks
        {
            get { return AppModules.Eksen + ".DistributedLocks"; }
        }
    }
}
