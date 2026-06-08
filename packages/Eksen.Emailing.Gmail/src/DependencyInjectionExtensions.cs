using Eksen.Emailing;
using Eksen.Emailing.Gmail;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class GmailEmailingBuilderExtensions
{
    extension(IEksenEmailingBuilder builder)
    {
        public IEksenEmailingBuilder UseGmail(string configSectionPath = GmailConfiguration.DefaultConfigSectionPath)
        {
            var services = builder.Services;

            services
                .AddOptions<GmailConfiguration>()
                .BindConfiguration(configSectionPath)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IEmailSender, GmailEmailSender>();

            return builder;
        }
    }
}
