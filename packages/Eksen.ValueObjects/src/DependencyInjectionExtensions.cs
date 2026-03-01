using Eksen.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddValueObjects(
        this IEksenBuilder builder,
        Action<IEksenValueObjectsBuilder>? configureAction = null)
    {
        var options = new EksenValueObjectOptions();
        options.AddAssembly(typeof(DependencyInjectionExtensions).Assembly);

        var ulidBuilder = new EksenValueObjectsBuilder(builder, options);

        if (configureAction != null)
        {
            configureAction(ulidBuilder);
        }

        return builder;
    }
}

public interface IEksenValueObjectsBuilder
{
    IEksenBuilder EksenBuilder { get; }

    EksenValueObjectOptions Options { get; }
}

public class EksenValueObjectsBuilder(IEksenBuilder eksenBuilder, EksenValueObjectOptions options)
    : IEksenValueObjectsBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;

    public EksenValueObjectOptions Options { get; } = options;
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