using Eksen.Captcha;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddCaptcha(
        this IEksenBuilder builder,
        Action<IEksenCaptchaBuilder>? configureAction = null)
    {
        var services = builder.Services;

        services
            .AddOptions<EksenCaptchaOptions>()
            .BindConfiguration(EksenCaptchaOptions.DefaultConfigSectionPath)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<ICaptchaValidator, BypassAwareCaptchaValidator>();

        if (configureAction != null)
        {
            var captchaBuilder = new EksenCaptchaBuilder(builder);
            configureAction(captchaBuilder);
        }

        return builder;
    }
}

public interface IEksenCaptchaBuilder
{
    IEksenBuilder EksenBuilder { get; }

    IEksenCaptchaBuilder Configure(Action<EksenCaptchaOptions> configureOptions);
}

public sealed class EksenCaptchaBuilder(IEksenBuilder eksenBuilder) : IEksenCaptchaBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;

    public IEksenCaptchaBuilder Configure(Action<EksenCaptchaOptions> configureOptions)
    {
        this.Services.Configure(configureOptions);
        return this;
    }
}

public static class EksenCaptchaBuilderExtensions
{
    extension(IEksenCaptchaBuilder builder)
    {
        public IServiceCollection Services
        {
            get { return builder.EksenBuilder.Services; }
        }
    }
}
