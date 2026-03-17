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

        services.AddEksen(eksen =>
        {
            eksen.AddErrorHandling(builder =>
            {
                builder.AddAspNetCoreSupport();
            });
        });

        // Act
        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IExceptionHandler));

        // Assert
        descriptor.ShouldNotBeNull();
        descriptor.ImplementationType.ShouldBe(typeof(EksenExceptionHandler));
    }
}
