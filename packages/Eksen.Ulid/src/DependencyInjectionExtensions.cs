using System.ComponentModel;
using Eksen.Ulid;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddUlid(
        this IEksenBuilder builder,
        Action<IEksenUlidBuilder>? configureAction = null)
    {
        builder.AddValueObjects(valueObjectsBuilder =>
        {
            valueObjectsBuilder.Configure(options =>
            {
                options.AddAssembly(typeof(UlidEntityId<>).Assembly);
            });
        });

        TypeDescriptor.AddAttributes(
            typeof(Ulid), 
            new TypeConverterAttribute(typeof(UlidTypeConverter)));

        if (configureAction != null)
        {
            var ulidBuilder = new EksenUlidBuilder(builder);
            configureAction(ulidBuilder);
        }

        return builder;
    }
}

public interface IEksenUlidBuilder
{
    IEksenBuilder EksenBuilder { get; }
}

public class EksenUlidBuilder(IEksenBuilder eksenBuilder)
    : IEksenUlidBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;
}

public static class EksenUlidBuilderExtensions
{
    extension(IEksenUlidBuilder builder)
    {
        public IServiceCollection Services
        {
            get { return builder.EksenBuilder.Services; }
        }
    }
}