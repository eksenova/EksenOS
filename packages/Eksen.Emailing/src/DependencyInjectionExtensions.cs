using Eksen.Emailing;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    extension(IEksenBuilder builder)
    {
        public IEksenEmailingBuilder AddEmailing(Action<IEksenEmailingBuilder>? configureAction = null)
        {
            builder.AddValueObjects(valueObjectBuilder =>
            {
                valueObjectBuilder.Configure(options =>
                {
                    options.AddAssembly(typeof(IEmailSender).Assembly);
                });
            });

            var emailBuilder = new EksenEmailingBuilder(builder);

            if (configureAction != null)
            {
                configureAction(emailBuilder);
            }

            return emailBuilder;
        }
    }

    extension(IEksenEmailingBuilder builder)
    {
        public IEksenEmailingBuilder AddEmailTemplates(string configSectionPath = SmtpConfiguration.DefaultConfigSectionPath)
        {
            var services = builder.Services;

            services.AddSingleton<ITemplateEmailSender, TemplateEmailSender>();

            return builder;
        }

        public IEksenEmailingBuilder UseSmtp(string configSectionPath = SmtpConfiguration.DefaultConfigSectionPath)
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

public interface IEksenEmailingBuilder
{
    IEksenBuilder EksenBuilder { get; }
}

public class EksenEmailingBuilder(IEksenBuilder eksenBuilder)
    : IEksenEmailingBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;
}

public static class EksenEmailingBuilderExtensions
{
    extension(IEksenEmailingBuilder builder)
    {
        public IServiceCollection Services
        {
            get { return builder.EksenBuilder.Services; }
        }
    }
}