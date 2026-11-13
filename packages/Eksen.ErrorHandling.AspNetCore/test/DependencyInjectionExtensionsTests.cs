using Eksen.TestBase;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Eksen.ErrorHandling.AspNetCore.Tests;

public class DependencyInjectionExtensionsTests : EksenUnitTestBase
{
    [Fact]
    public void AddAspNetCoreSupport_Should_Register_ExceptionHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddEksen(eksen =>
        {
            eksen.AddLocalization().AddErrorHandling(builder =>
            {
                builder.AddAspNetCoreSupport();
            });
        });

        // Act
        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IExceptionHandler));

        // Assert
        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        provider.GetServices<IExceptionHandler>().Single().ShouldBeOfType<EksenExceptionHandler>();
    }
}
