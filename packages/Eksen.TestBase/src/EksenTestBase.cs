using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Eksen.TestBase;

/// <summary>
/// Base class for simple unit tests that do not require dependency injection.
/// </summary>
public abstract class EksenUnitTestBase;

/// <summary>
/// Base class for tests that require a service provider built from <see cref="IEksenBuilder"/>.
/// Override <see cref="ConfigureServices"/> to register additional services, and
/// <see cref="ConfigureEksen"/> to configure Eksen framework services.
/// </summary>
public abstract class EksenServiceTestBase : IAsyncLifetime
{
    public IServiceProvider ServiceProvider { get; private set; } = null!;

    public virtual async Task InitializeAsync()
    {
        var services = new ServiceCollection();

        services.AddEksen(eksenBuilder =>
        {
            eksenBuilder.AddUnitOfWork();
            ConfigureEksen(eksenBuilder);
        });

        await ConfigureServices(services);

        ServiceProvider = services.BuildServiceProvider();

        await OnServiceProviderBuiltAsync();
    }

    protected virtual Task ConfigureServices(ServiceCollection services)
    {
        return Task.CompletedTask;
    }

    protected virtual void ConfigureEksen(IEksenBuilder builder)
    {
    }

    protected virtual Task OnServiceProviderBuiltAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}