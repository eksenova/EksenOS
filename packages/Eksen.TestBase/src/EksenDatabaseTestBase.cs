using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Eksen.TestBase;

/// <summary>
/// Base class for integration tests that require a real database backed by Testcontainers.
/// Subclasses provide the connection string and EF Core provider configuration.
/// </summary>
public abstract class EksenDatabaseTestBase<TDbContext> : EksenServiceTestBase
    where TDbContext : DbContext
{
    protected abstract Task<string> GetConnectionStringAsync();

    protected abstract void ConfigureDbContext(
        IEksenEntityFrameworkCoreBuilder efCoreBuilder,
        string connectionString);

    protected override async Task ConfigureServices(ServiceCollection services)
    {
        await base.ConfigureServices(services);

        services.AddScoped<ITestEntityRepository, TestEntityRepository>();

        var connectionString = await GetConnectionStringAsync();

        services.AddEksen(eksenBuilder =>
        {
            eksenBuilder.AddEntityFrameworkCore(efCoreBuilder =>
            {
                ConfigureDbContext(efCoreBuilder, connectionString);
            });
        });
    }

    protected override async Task OnServiceProviderBuiltAsync()
    {
        await base.OnServiceProviderBuiltAsync();

        var dbContext = ServiceProvider.GetRequiredService<TDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }
}
