using Eksen.Localization.Formatting;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddLocalization(this IEksenBuilder eksenBuilder)
    {
        var services = eksenBuilder.Services;

        services.AddSingleton<IMessageFormatter, SmartFormatMessageFormatter>();

        return eksenBuilder;
    }
}