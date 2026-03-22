using Eksen.Core;

namespace Eksen.Authentication.ApiKeys;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.AuthenticationApiKeys);
    }

    extension(AppModules)
    {
        public static string AuthenticationApiKeys
        {
            get { return AppModules.Eksen + ".Authentication.ApiKeys"; }
        }
    }
}
