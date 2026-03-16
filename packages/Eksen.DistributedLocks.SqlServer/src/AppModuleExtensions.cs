using Eksen.Core;

namespace Eksen.DistributedLocks.SqlServer;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.DistributedLocksSqlServer);
    }

    extension(AppModules)
    {
        public static string DistributedLocksSqlServer
        {
            get { return AppModules.DistributedLocks + ".SqlServer"; }
        }
    }
}
