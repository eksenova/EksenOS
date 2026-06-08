using Eksen.EntityFrameworkCore;
using Eksen.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenEntityFrameworkCoreBuilder UseSqliteDbContext<TDbContext>(
        this IEksenEntityFrameworkCoreBuilder builder,
        string connectionString,
        Action<DbContextOptionsBuilder<TDbContext>>? dbContextOptionsAction = null,
        Action<SqliteDbContextOptionsBuilder>? sqliteOptionsAction = null)
        where TDbContext : DbContext
    {
        var services = builder.Services;

        services.AddScoped<DbContextOptions<TDbContext>>(serviceProvider =>
        {
            var dbContextOptionsBuilder = new DbContextOptionsBuilder<TDbContext>();
            dbContextOptionsBuilder.UseSqlite(connectionString, sqliteOptionsAction);
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
