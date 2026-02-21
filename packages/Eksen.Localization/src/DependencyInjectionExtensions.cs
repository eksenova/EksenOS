using Eksen.Localization.Formatting;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddEksenLocalization(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IMessageFormatter, SmartFormatMessageFormatter>();

        return serviceCollection;
    }
}