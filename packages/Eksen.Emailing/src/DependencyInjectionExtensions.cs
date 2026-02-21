using Eksen.Emailing;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddEmailTemplates(
        this IServiceCollection services,
        string configSectionPath = SmtpConfiguration.DefaultConfigSectionPath)
    {
        services.AddSingleton<ITemplateEmailSender, TemplateEmailSender>();

        return services;
    }

    public static IServiceCollection AddSmtpEmailing(
        this IServiceCollection services,
        string configSectionPath = SmtpConfiguration.DefaultConfigSectionPath)
    {
        services
            .AddOptions<SmtpConfiguration>()
            .BindConfiguration(configSectionPath)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        return services;
    }
}