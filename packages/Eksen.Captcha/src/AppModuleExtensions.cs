using Eksen.Core;

namespace Eksen.Captcha;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.Captcha);
    }

    extension(AppModules)
    {
        public static string Captcha
        {
            get { return AppModules.Eksen + ".Captcha"; }
        }
    }
}
