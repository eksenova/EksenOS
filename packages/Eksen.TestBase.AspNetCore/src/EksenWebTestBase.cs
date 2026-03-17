using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Eksen.TestBase.AspNetCore;

/// <summary>
/// Base class for end-to-end tests that use an ASP.NET Core <see cref="WebApplicationFactory{TEntryPoint}"/>
/// backed by a real database via <see cref="EksenDatabaseTestBase{TDbContext}"/>.
/// <para>
/// <typeparamref name="TProgram"/> is the application entry point (typically <c>Program</c>).
/// <typeparamref name="TDbContext"/> is the EF Core context that the application uses.
/// </para>
/// <para>
/// Subclasses must provide a connection string (e.g. from a Testcontainer fixture)
/// and configure the EF Core provider for that database via <see cref="ConfigureDbContext"/>.
/// Additional web host customization can be done by overriding <see cref="ConfigureWebHost"/>.
/// </para>
/// </summary>
public abstract class EksenWebTestBase<TProgram, TDbContext> : IAsyncLifetime
    where TProgram : class
    where TDbContext : DbContext
{
    private WebApplicationFactory<TProgram>? _factory;

    public HttpClient Client { get; private set; } = null!;

    protected WebApplicationFactory<TProgram> Factory =>
        _factory ?? throw new InvalidOperationException("WebApplicationFactory has not been initialized yet.");

    protected abstract Task<string> GetConnectionStringAsync();

    protected abstract void ConfigureDbContext(
        IEksenEntityFrameworkCoreBuilder efCoreBuilder,
        string connectionString);

    protected virtual void ConfigureWebHost(IWebHostBuilder builder)
    {
    }

    public virtual async Task InitializeAsync()
    {
        var connectionString = await GetConnectionStringAsync();

        // Pre-create the database before building the factory so that data seeders
        // executed during host startup find the schema already in place.
        await EnsureDatabaseCreatedAsync(connectionString);

        _factory = new WebApplicationFactory<TProgram>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    RemoveDbContextRegistrations<TDbContext>(services);

                    services.AddEksen(eksenBuilder =>
                    {
                        eksenBuilder.AddEntityFrameworkCore(efCoreBuilder =>
                        {
                            ConfigureDbContext(efCoreBuilder, connectionString);
                        });
                    });
                });

                ConfigureWebHost(builder);
            });

        Client = _factory.CreateClient();
    }

    public virtual async Task DisposeAsync()
    {
        Client.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private static void RemoveDbContextRegistrations<TContext>(IServiceCollection services)
        where TContext : DbContext
    {
        var descriptors = services
            .Where(d =>
                d.ServiceType == typeof(TContext) ||
                d.ServiceType == typeof(DbContextOptions<TContext>) ||
                d.ServiceType.FullName == "Eksen.UnitOfWork.IUnitOfWorkProvider" ||
                d.ServiceType.FullName == "Eksen.EntityFrameworkCore.IDbContextTracker")
            .ToList();

        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }

    protected virtual async Task EnsureDatabaseCreatedAsync(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();
        ConfigureDbContextOptions(optionsBuilder, connectionString);

        var dbContext = (TDbContext)Activator.CreateInstance(
            typeof(TDbContext),
            optionsBuilder.Options,
            null)!;
        await using (dbContext)
        {
            await dbContext.Database.EnsureCreatedAsync();
        }
    }

    protected virtual void ConfigureDbContextOptions(
        DbContextOptionsBuilder<TDbContext> optionsBuilder,
        string connectionString)
    {
    }
}
