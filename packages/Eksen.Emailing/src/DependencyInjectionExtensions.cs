using Eksen.Emailing;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    extension(IEksenBuilder builder)
    {
        public IEksenBuilder AddEmailTemplates(string configSectionPath = SmtpConfiguration.DefaultConfigSectionPath)
        {
            var services = builder.Services;

            services.AddSingleton<ITemplateEmailSender, TemplateEmailSender>();

            return builder;
        }

        public IEksenBuilder AddSmtpEmailing(string configSectionPath = SmtpConfiguration.DefaultConfigSectionPath)
        {
            var services = builder.Services;

            services
                .AddOptions<SmtpConfiguration>()
                .BindConfiguration(configSectionPath)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IEmailSender, SmtpEmailSender>();

            return builder;
        }
    }
}