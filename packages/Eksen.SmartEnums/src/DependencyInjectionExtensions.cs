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
        builder.Services.Configure<SmartEnumOptions>(options =>
        {
            options.AddAssembly(typeof(DependencyInjectionExtensions).Assembly);

            if (configureAction != null)
            {
                var smartEnumsBuilder = new EksenSmartEnumsBuilder(builder, options);
                configureAction(smartEnumsBuilder);
            }
        });

        return builder;
    }
}

public interface IEksenSmartEnumsBuilder
{
    IEksenBuilder EksenBuilder { get; }

    SmartEnumOptions Options { get; }
}

public class EksenSmartEnumsBuilder(IEksenBuilder eksenBuilder, SmartEnumOptions options)
    : IEksenSmartEnumsBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;

    public SmartEnumOptions Options { get; } = options;
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