using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Eksen.TestBase;

public class EksenTestBase : IAsyncLifetime
{
    public virtual async Task InitializeAsync()
    {
        var services = new ServiceCollection();

        services.AddEksen(eksenBuilder =>
        {
            eksenBuilder.AddUnitOfWork();
        });
        services.AddScoped<ITestEntityRepository, TestEntityRepository>();

        await BuildServicesAsync(services);

        ServiceProvider = services.BuildServiceProvider();
    }

    protected virtual Task BuildServicesAsync(ServiceCollection services)
    {
        return Task.CompletedTask;
    }

    public IServiceProvider ServiceProvider { get; set; } = null!;

    public virtual Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}