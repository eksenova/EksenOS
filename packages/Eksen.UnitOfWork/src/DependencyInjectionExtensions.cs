using Eksen.UnitOfWork;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddUnitOfWork(
        this IEksenBuilder builder
    )
    {
        var services = builder.Services;
        services.AddScoped<IUnitOfWorkManager, UnitOfWorkManager>();
        return builder;
    }
}