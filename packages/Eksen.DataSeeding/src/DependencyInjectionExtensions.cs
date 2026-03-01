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

            services.Configure<EksenDataSeedingOptions>(options =>
            {
                if (configureAction != null)
                {
                    var dataSeedingBuilder = new EksenDataSeedingBuilder(builder, options);
                    configureAction(dataSeedingBuilder);
                }
            });

            return builder;
        }
    }
}

public interface IEksenDataSeedingBuilder
{
    IEksenBuilder EksenBuilder { get; }

    EksenDataSeedingOptions Options { get; }
}

public class EksenDataSeedingBuilder(IEksenBuilder eksenBuilder, EksenDataSeedingOptions options)
    : IEksenDataSeedingBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;

    public EksenDataSeedingOptions Options { get; } = options;
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