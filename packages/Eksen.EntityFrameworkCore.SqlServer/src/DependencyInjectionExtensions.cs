using Eksen.EntityFrameworkCore;
using Eksen.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenEntityFrameworkCoreBuilder UseSqlServerDbContext<TDbContext>(
        this IEksenEntityFrameworkCoreBuilder builder,
        string connectionName = "Default",
        Action<DbContextOptionsBuilder<TDbContext>>? dbContextOptionsAction = null,
        Action<SqlServerDbContextOptionsBuilder>? sqlServerOptionsAction = null)
        where TDbContext : DbContext
    {
        var services = builder.Services;

        services.AddScoped<DbContextOptions<TDbContext>>(serviceProvider =>
        {
            var dbContextOptionsBuilder = new DbContextOptionsBuilder<TDbContext>();
            dbContextOptionsBuilder.UseSqlServer(connectionName, sqlServerOptionsAction);
            dbContextOptionsAction?.Invoke(dbContextOptionsBuilder);

            var dbContextTracker = serviceProvider.GetRequiredService<IDbContextTracker>();
            var unitOfWorkManager = serviceProvider.GetRequiredService<IUnitOfWorkManager>();

            dbContextOptionsBuilder.AddInterceptors(
                new AutoPropertiesSaveChangesInterceptor(),
                new DbContextTrackerCommandInterceptor(unitOfWorkManager, dbContextTracker),
                new DbContextTrackerSaveChangesInterceptor(unitOfWorkManager, dbContextTracker));

            return dbContextOptionsBuilder
                .Options;
        });

        services.AddScoped<TDbContext>();

        return builder;
    }
}