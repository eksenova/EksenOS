using Eksen.EntityFrameworkCore;
using Eksen.UnitOfWork;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static void AddEksenEntityFrameworkCoreIntegration(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWorkProvider, EfCoreUnitOfWorkProvider>();
        services.AddScoped<IDbContextTracker, DbContextTracker>();
    }
}