using Eksen.SmartEnums;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddSmartEnums(
        this IEksenBuilder builder,
        Action<IEksenSmartEnumsBuilder>? configureAction = null)
    {
        var smartEnumsBuilder = new EksenSmartEnumsBuilder(builder);
        smartEnumsBuilder.Configure(options =>
        {
            options.AddAssembly(typeof(Enumeration<>).Assembly);
        });

        if (configureAction != null)
        {
            configureAction(smartEnumsBuilder);
        }

        return builder;
    }
}

public interface IEksenSmartEnumsBuilder
{
    IEksenBuilder EksenBuilder { get; }

    IEksenSmartEnumsBuilder Configure(Action<EksenSmartEnumOptions> configureOptions);
}

public class EksenSmartEnumsBuilder(IEksenBuilder eksenBuilder)
    : IEksenSmartEnumsBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;

    public IEksenSmartEnumsBuilder Configure(Action<EksenSmartEnumOptions> configureOptions)
    {
        this.Services.Configure(configureOptions);
        return this;
    }
}

public static class EksenSmartEnumsBuilderExtensions
{
    extension(IEksenSmartEnumsBuilder builder)
    {
        public IServiceCollection Services
        {
            get { return builder.EksenBuilder.Services; }
        }
    }
}