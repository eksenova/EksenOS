using Eksen.Captcha;
using Eksen.Captcha.Cloudflare;
using Eksen.Cloudflare;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    extension(IEksenCaptchaBuilder builder)
    {
        public IEksenCaptchaBuilder UseCloudflareTurnstile(
            Action<CloudflareTurnstileOptions>? configureOptions = null,
            string configSectionPath = CloudflareTurnstileOptions.DefaultConfigSectionPath)
        {
            var services = builder.Services;

            var optionsBuilder = services
                .AddOptions<CloudflareTurnstileOptions>()
                .BindConfiguration(configSectionPath)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            if (configureOptions != null)
            {
                optionsBuilder.Configure(configureOptions);
            }

            services.AddHttpClient(CloudflareDefaults.TurnstileHttpClientName);

            services.RemoveAll<ICaptchaProvider>();
            services.AddSingleton<ICaptchaProvider, CloudflareTurnstileCaptchaProvider>();

            return builder;
        }
    }
}
