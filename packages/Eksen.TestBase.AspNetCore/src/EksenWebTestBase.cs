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
/// <para>
/// By default every test instance builds and owns its factory. Subclasses that share one booted host
/// across many tests instead override <see cref="InitializeAsync"/>, build the shared factory once via
/// <see cref="BuildFactory"/>, attach it per test with <see cref="AttachFactory"/>, and return
/// <see langword="false"/> from <see cref="OwnsFactory"/> so disposal leaves the shared host running.
/// </para>
/// </summary>
public abstract class EksenWebTestBase<TProgram, TDbContext> : IAsyncLifetime
    where TProgram : class
    where TDbContext : DbContext
{
    private WebApplicationFactory<TProgram>? _factory;

    public HttpClient Client { get; protected set; } = null!;

    protected WebApplicationFactory<TProgram> Factory =>
        _factory ?? throw new InvalidOperationException("WebApplicationFactory has not been initialized yet.");

    /// <summary>
    /// Whether this test instance owns <see cref="Factory"/> and disposes it in
    /// <see cref="DisposeAsync"/>. Subclasses sharing one host across tests return <see langword="false"/>.
    /// </summary>
    protected virtual bool OwnsFactory => true;

    protected abstract Task<string> GetConnectionStringAsync();

    protected abstract void ConfigureDbContext(
        IEksenEntityFrameworkCoreBuilder efCoreBuilder,
        string connectionString);

    protected virtual void ConfigureWebHost(IWebHostBuilder builder)
    {
    }

    public virtual async ValueTask InitializeAsync()
    {
        var connectionString = await GetConnectionStringAsync();

        // Pre-create the database before building the factory so that data seeders
        // executed during host startup find the schema already in place.
        await EnsureDatabaseCreatedAsync(connectionString);

        _factory = BuildFactory(connectionString);
        Client = _factory.CreateClient();
    }

    /// <summary>
    /// Attaches an already-built factory to this test instance instead of building one, for subclasses
    /// that share a single booted host across many tests.
    /// </summary>
    protected void AttachFactory(WebApplicationFactory<TProgram> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Builds the test host factory against <paramref name="connectionString"/>: the application's own
    /// <typeparamref name="TDbContext"/> registrations are stripped and re-registered through the Eksen
    /// builder pointing at the test database, then <see cref="ConfigureWebHost"/> applies the subclass's
    /// customizations.
    /// </summary>
    protected WebApplicationFactory<TProgram> BuildFactory(string connectionString)
    {
        return new WebApplicationFactory<TProgram>()
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
    }

    public virtual async ValueTask DisposeAsync()
    {
        Client.Dispose();

        if (_factory is not null && OwnsFactory)
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
