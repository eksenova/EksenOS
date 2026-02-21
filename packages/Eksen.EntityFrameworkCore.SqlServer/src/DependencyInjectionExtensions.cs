using Eksen.EntityFrameworkCore;
using Eksen.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static void AddEksenSqlServerDbContext<TDbContext>(
        this IServiceCollection services,
        string connectionName = "Default",
        Action<DbContextOptionsBuilder<TDbContext>>? dbContextOptionsAction = null,
        Action<SqlServerDbContextOptionsBuilder>? sqlServerOptionsAction = null)
        where TDbContext : DbContext
    {
        services.AddEksenEntityFrameworkCoreIntegration();

        services.AddScoped<DbContextOptions<TDbContext>>(serviceProvider =>
        {
            var builder = new DbContextOptionsBuilder<TDbContext>();
            builder.UseSqlServer(connectionName, sqlServerOptionsAction);
            dbContextOptionsAction?.Invoke(builder);

            var dbContextTracker = serviceProvider.GetRequiredService<IDbContextTracker>();
            var unitOfWorkManager = serviceProvider.GetRequiredService<IUnitOfWorkManager>();

            builder.AddInterceptors(
                new AutoPropertiesSaveChangesInterceptor(),
                new DbContextTrackerCommandInterceptor(unitOfWorkManager, dbContextTracker),
                new DbContextTrackerSaveChangesInterceptor(unitOfWorkManager, dbContextTracker));

            return builder
                .Options;
        });
        services.AddScoped<TDbContext>();
    }
}