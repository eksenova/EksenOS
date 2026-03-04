using Eksen.ValueObjects;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddValueObjects(
        this IEksenBuilder builder,
        Action<IEksenValueObjectsBuilder>? configureAction = null)
    {
        var valueObjectBuilder = new EksenValueObjectsBuilder(builder);
        valueObjectBuilder.Configure(options =>
        {
            options.AddAssembly(typeof(ValueObject<,>).Assembly);
        });

        if (configureAction != null)
        {
            configureAction(valueObjectBuilder);
        }

        return builder;
    }
}

public interface IEksenValueObjectsBuilder
{
    IEksenBuilder EksenBuilder { get; }

    IEksenValueObjectsBuilder Configure(Action<EksenValueObjectOptions> configureOptions);
}

public class EksenValueObjectsBuilder(IEksenBuilder eksenBuilder) : IEksenValueObjectsBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;

    public IEksenValueObjectsBuilder Configure(Action<EksenValueObjectOptions> configureOptions)
    {
        var eager = new EksenValueObjectOptions();
        configureOptions(eager);

        this.Services.Configure(configureOptions);
        return this;
    }
}

public static class EksenValueObjectsBuilderExtensions
{
    extension(IEksenValueObjectsBuilder builder)
    {
        public IServiceCollection Services
        {
            get { return builder.EksenBuilder.Services; }
        }
    }
}