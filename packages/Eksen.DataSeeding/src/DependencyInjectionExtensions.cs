using Eksen.DataSeeding;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    extension(IEksenBuilder builder)
    {
        public IEksenBuilder AddDataSeeding(Action<IEksenDataSeedingBuilder>? configureAction = null)
        {
            var services = builder.Services;
            services.AddSingleton<IDataSeeder, DataSeeder>();

            if (configureAction != null)
            {
                var dataSeedingBuilder = new EksenDataSeedingBuilder(builder);
                configureAction(dataSeedingBuilder);
            }

            return builder;
        }
    }
}

public interface IEksenDataSeedingBuilder
{
    IEksenBuilder EksenBuilder { get; }

    IEksenDataSeedingBuilder Configure(Action<EksenDataSeedingOptions> configureOptions);
}

public class EksenDataSeedingBuilder(IEksenBuilder eksenBuilder)
    : IEksenDataSeedingBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;

    public IEksenDataSeedingBuilder Configure(Action<EksenDataSeedingOptions> configureOptions)
    {
        this.Services.Configure(configureOptions);
        return this;
    }
}

public static class EksenDataSeedingBuilderExtensions
{
    extension(IEksenDataSeedingBuilder builder)
    {
        public IServiceCollection Services
        {
            get { return builder.EksenBuilder.Services; }
        }
    }
}