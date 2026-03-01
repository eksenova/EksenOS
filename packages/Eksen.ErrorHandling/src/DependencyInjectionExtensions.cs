using Eksen.ErrorHandling;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddErrorHandling(
        this IEksenBuilder builder,
        Action<IEksenErrorHandlingBuilder>? configureAction = null)
    {
        var services = builder.Services;

        services.AddScoped<IErrorMessageTemplateResolver, NullErrorMessageTemplateResolver>();
        services.AddScoped<IErrorFormatter, ErrorFormatter>();

        if (configureAction != null)
        {
            var errorHandlingBuilder = new EksenErrorHandlingBuilder(builder);
            configureAction(errorHandlingBuilder);
        }

        return builder;
    }
}

public interface IEksenErrorHandlingBuilder
{
    IEksenBuilder EksenBuilder { get; }
}

public class EksenErrorHandlingBuilder(IEksenBuilder eksenBuilder)
    : IEksenErrorHandlingBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;
}

public static class EksenErrorHandlingBuilderExtensions
{
    extension(IEksenErrorHandlingBuilder builder)
    {
        public IServiceCollection Services
        {
            get { return builder.EksenBuilder.Services; }
        }
    }
}