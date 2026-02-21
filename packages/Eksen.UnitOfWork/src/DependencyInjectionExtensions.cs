using Eksen.UnitOfWork;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static void AddUnitOfWork(
        this IServiceCollection services
    )
    {
        services.AddScoped<IUnitOfWorkManager, UnitOfWorkManager>();
    }
}