using Eksen.ErrorHandling.AspNetCore;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenErrorHandlingBuilder AddAspNetCoreSupport(
        this IEksenErrorHandlingBuilder builder)
    {
        var services = builder.Services;

        services.AddExceptionHandler<EksenExceptionHandler>();

        return builder;
    }
}