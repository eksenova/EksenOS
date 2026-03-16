using Eksen.Core;

namespace Eksen.DistributedTransactions;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.DistributedTransactions);
    }

    extension(AppModules)
    {
        public static string DistributedTransactions
        {
            get { return AppModules.Eksen + ".DistributedTransactions"; }
        }
    }
}
